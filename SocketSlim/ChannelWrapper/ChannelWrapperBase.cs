using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace SocketSlim.ChannelWrapper
{
    /// <summary>
    /// This class wraps a socket and allows for sending and receiving of the data on that socket.
    /// 
    /// The socket which is taken by the constructor is presumed as being open.
    /// 
    /// Typical workflow with this class should be as follows:
    /// * Create an object with the constructor;
    /// * Subscribe for <see cref="BytesReceived"/> and <see cref="Closed"/> events;
    /// * Call <see cref="Start"/> method;
    /// * Handle incoming data through <see cref="BytesReceived"/> event handler and call <see cref="Send"/> to send data;
    /// * Optionally call <see cref="Close"/> method to close the connection.
    /// * Once the <see cref="Closed"/> event is raised, the socket is closed;
    /// </summary>
    public class ChannelWrapperBase
    {
        // todo: provide socket close reason when calling Closed event

        private readonly Socket socket;

        private readonly SocketAsyncEventArgs receiver;
        private readonly DirectBytesReceivedEventArgs receivedEventArgs;

        private readonly SocketAsyncEventArgs sender;
        private readonly MemoryStream sendBufferWriter;
        private readonly int outgoingBufferOffset;

        private readonly Queue<byte[]> outgoingQueue = new Queue<byte[]>(10);

        private bool sending;
        private byte[] currentMessage;
        private int currentMessageOffset;

        private int freeSendCounter;
        private int freeReceiveCounter;

        private bool closed;

        /// <summary>
        /// Creates the non-reusable socket wrapper.
        /// </summary>
        /// <param name="socket">socket channel to use for communications</param>
        /// <param name="receiver"><see cref="SocketAsyncEventArgs"/> to be used for receiving, it should have its buffer already set up</param>
        /// <param name="receivedEventArgs"><see cref="DirectBytesReceivedEventArgs"/> object to be used for <see cref="BytesReceived"/> event notifications (should point to <paramref name="receiver"/>)</param>
        /// <param name="sender"><see cref="SocketAsyncEventArgs"/> to be used for sending, it should have its buffer already set up</param>
        /// <param name="sendBufferWriter"><see cref="MemoryStream"/> to be used for writing the data to sending buffer. It should already point to <paramref name="sender"/>'s buffer</param>
        /// <remarks>All these weird arguments allow for preallocation and pooling of <see cref="SocketAsyncEventArgs"/> and other related resources</remarks>
        public ChannelWrapperBase(Socket socket, SocketAsyncEventArgs receiver, DirectBytesReceivedEventArgs receivedEventArgs, SocketAsyncEventArgs sender, MemoryStream sendBufferWriter)
        {
            this.socket = socket;
            
            this.receiver = receiver;
            receiver.Completed += OnReceiveCompleted;
            this.receivedEventArgs = receivedEventArgs;
            receivedEventArgs.ProceedReceive += OnProceedReceive;

            this.sender = sender;
            sender.Completed += OnSendCompleted;
            this.sendBufferWriter = sendBufferWriter;
            
            outgoingBufferOffset = sender.Offset;
        }

        /// <summary>
        /// Starts the receive loop. It's not started in the constructor so you can subscribe for proper events first.
        /// </summary>
        public void Start()
        {
            StartReceive();
        }

        private bool StartReceive()
        {
            bool callbackPending = false, socketClosed = false;
            try
            {
                callbackPending = socket.ReceiveAsync(receiver);
            }
            catch (Exception) { socketClosed = true; }
            
            if (socketClosed)
            {
                CloseSocket(isReceiver: true); // finish execution
                return false;
            }

            if (!callbackPending)
            {
                return ProcessReceive();
            }

            return true;
        }

        private void OnReceiveCompleted(object o, SocketAsyncEventArgs e)
        {
            ProcessReceive();
        }

        private bool ProcessReceive()
        {
            bool socketClosed = false;
            try
            {
                if (receiver.SocketError != SocketError.Success || receiver.BytesTransferred == 0)
                {
                    CloseSocket(isReceiver: true);
                    return false;
                }

                RaiseBytesReceived();
            }
            catch (Exception)
            {
                socketClosed = true;
            }

            if (socketClosed)
            {
                CloseSocket(isReceiver: true);
                return false;
            }

            return true;
        }

        private void OnProceedReceive(object o, ProceedReceiveEventArgs e)
        {
            if (!StartReceive())
            {
                e.Closed = true;
            }
        }

        /// <summary>
        /// This event is fired when the socket receives some bytes.
        /// 
        /// The handler <b>MUST</b> call the <see cref="BytesReceivedEventArgs.Proceed"/> method to continue receiving data.
        /// </summary>
        public event EventHandler<BytesReceivedEventArgs> BytesReceived;

        protected virtual void RaiseBytesReceived()
        {
            EventHandler<BytesReceivedEventArgs> handler = BytesReceived;
            if (handler != null)
            {
                handler(this, receivedEventArgs);
            }
        }

        public void Send(byte[] msg)
        {
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Length == 0) throw new ArgumentOutOfRangeException("msg", msg.Length, "Message should not be empty (sending empty array is equivalent to not sending anything at all)");

            if (closed)
            {
                throw new InvalidOperationException("Cannot send data on closed connection");
            }

            bool needStart = false;
            lock (outgoingQueue)
            {
                outgoingQueue.Enqueue(msg);

                if (!sending)
                {
                    sending = true;
                    needStart = true;
                }
            }

            if (needStart)
            {
                StartSend();
            }
        }

        private void StartSend()
        {
            const int preallocatedArrayLength = 10;

            List<byte[]> messages;
            int bytesToSend;
            bool needQueue = true;

            sendBufferWriter.Seek(0, SeekOrigin.Begin);

            if (currentMessage != null)
            {
                // handle the remaining message from the previous sending
                if (currentMessage.Length - currentMessageOffset > sendBufferWriter.Length)
                {
                    messages = new List<byte[]>();
                    
                    bytesToSend = (int) sendBufferWriter.Length;
                    sendBufferWriter.Write(currentMessage, currentMessageOffset, bytesToSend);
                    
                    currentMessageOffset += bytesToSend;

                    needQueue = false;
                }
                else
                {
                    messages = new List<byte[]>(preallocatedArrayLength);
                    
                    bytesToSend = currentMessage.Length - currentMessageOffset;
                    sendBufferWriter.Write(currentMessage, currentMessageOffset, bytesToSend);

                    currentMessage = null;
                }
            }
            else
            {
                messages = new List<byte[]>(preallocatedArrayLength);
                bytesToSend = 0;
            }

            if (needQueue)
            {
                lock (outgoingQueue)
                {
                    if (bytesToSend == 0 && outgoingQueue.Count == 0)
                    {
                        sending = false;
                        return;
                    }

                    // fill in the send buffer
                    while (outgoingQueue.Count > 0)
                    {
                        byte[] message = outgoingQueue.Dequeue();
                        messages.Add(message);

                        if (bytesToSend + message.Length > sendBufferWriter.Length)
                        {
                            // message doesn't fit into buffer
                            currentMessage = message;
                            currentMessageOffset = (int) (sendBufferWriter.Length - bytesToSend);

                            break;
                        }

                        // message fits into the remaining buffer space
                        bytesToSend += message.Length;
                    }
                }
            }

            // fill in the send buffer
            foreach (byte[] message in messages)
            {
                if (message == currentMessage)
                {
                    // message doesn't fit into buffer
// ReSharper disable AssignNullToNotNullAttribute
                    sendBufferWriter.Write(currentMessage, 0, currentMessageOffset);
// ReSharper restore AssignNullToNotNullAttribute

                    break;
                }

                // message fits into the remaining buffer space
                sendBufferWriter.Write(message, 0, message.Length);
            }

            // set the right buffer boundary
            sender.SetBuffer(outgoingBufferOffset, (int) sendBufferWriter.Position);

            StartAsyncSend();
        }

        private void StartAsyncSend()
        {
            bool callbackPending = false, socketClosed = false;
            try
            {
                callbackPending = socket.SendAsync(sender);
            }
            catch (Exception)
            {
                socketClosed = true;
            }

            if (socketClosed)
            {
                CloseSocket(isReceiver: false);
                return;
            }

            if (!callbackPending)
            {
                ProcessSent();
            }
        }

        private void OnSendCompleted(object o, SocketAsyncEventArgs e)
        {
            ProcessSent();
        }

        private void ProcessSent()
        {
            bool socketClosed = false;
            try
            {
                if (sender.SocketError != SocketError.Success)
                {
                    CloseSocket(isReceiver: false);
                    return;
                }

                StartSend();
            }
            catch (Exception)
            {
                socketClosed = true;
            }

            if (socketClosed)
            {
                CloseSocket(isReceiver: false);
            }
        }

        private void CloseInternal()
        {
            if (closed)
            {
                return;
            }
           
            closed = true;

            try
            {
                // do a shutdown before you close the socket
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }

                // close socket
                socket.Close();
            }
            catch (NullReferenceException) { }
        }

        private void CloseSocket(bool isReceiver)
        {
            bool freeSend = false;
            if (!isReceiver)
            {
                freeSend = Interlocked.Increment(ref freeSendCounter) == 1;
            }
            else
            {
                if (!sending)
                {
                    freeSend = Interlocked.Increment(ref freeSendCounter) == 1;
                }
            }

            bool freeReceive = isReceiver && (Interlocked.Increment(ref freeReceiveCounter) == 1);

            if (!freeSend && !freeReceive)
            {
                return; // nothing to do
            }

            CloseInternal();

            if (freeSend)
            {
                sender.Completed -= OnSendCompleted;
            }

            if (freeReceive)
            {
                receiver.Completed -= OnReceiveCompleted;
                receivedEventArgs.ProceedReceive -= OnProceedReceive;
                RaiseClosed();
            }
        }

        /// <summary>
        /// Event is fired when the socket is closed.
        /// </summary>
        public event EventHandler Closed;

        protected virtual void RaiseClosed()
        {
            EventHandler handler = Closed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public void Close()
        {
            // close the socket itself, both receiver and sender will quickly return errors and free themselves shortly after that
            CloseInternal();
        }
    }
}