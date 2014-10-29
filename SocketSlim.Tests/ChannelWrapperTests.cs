using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SocketSlim.ChannelWrapper;
using SocketSlim.Client;
using Xunit;
using System.Linq;

namespace SocketSlim.Tests
{
    public class ChannelWrapperTests : ClientConnectorTestsBase
    {
        private readonly ChannelWrapperBase wrapper;
        private readonly Socket otherEnd;

        private readonly byte[] receiveBuffer = new byte[17];
        private readonly SocketAsyncEventArgs receiver;
        private readonly DirectBytesReceivedEventArgs receiverArgs;

        private readonly byte[] sendBuffer = new byte[11];
        private readonly SocketAsyncEventArgs sender;
        private readonly MemoryStream sendBufferWriter;

        private readonly ManualResetEventSlim closedEvent = new ManualResetEventSlim(false);

        private readonly ConcurrentDictionary<DuplexSide, ChannelCloseEventArgs> closeErrorData = new ConcurrentDictionary<DuplexSide, ChannelCloseEventArgs>();

        private int bytesReceived;
        private readonly MemoryStream receivedData = new MemoryStream();

        public ChannelWrapperTests()
            : this(new TaskClientConnector(SocketType.Stream, ProtocolType.Tcp, new SocketAsyncEventArgs())
                       {
                           Address = Addr,
                           Port = Port
                       })
        { }

        public ChannelWrapperTests(TaskClientConnector connector) : base(connector)
        {
            // init various things
            receiver = new SocketAsyncEventArgs();
            receiver.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
            receiverArgs = new DirectBytesReceivedEventArgs(receiver);

            sender = new SocketAsyncEventArgs();
            sender.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            sendBufferWriter = new MemoryStream(sendBuffer, true);

            // establish a connection
            Task<Socket> task = connector.ConnectAsync();
            task.Wait(TimeSpan.FromSeconds(2));

            wrapper = new ChannelWrapperBase(task.Result, receiver, receiverArgs, sender, sendBufferWriter);

            WaitForServerConnections(1);

            // get the other end of the socket
            otherEnd = Connections[0];

            // set up event handlers
            wrapper.BytesReceived += OnBytesReceived;
            wrapper.Closed += OnClosed;
            wrapper.DuplexChannelClosed += OnDuplexChannelClosed;

            // start receive loop
            wrapper.Start();
        }

        private void OnBytesReceived(object o, ChannelWrapper.BytesReceivedEventArgs e)
        {
            Interlocked.Add(ref bytesReceived, e.Size);

            receivedData.Write(e.Buffer, e.Offset, e.Size);

            // without proceed nothing works :)
            e.Proceed();
        }

        private void WaitForReceivedData(int bytesCount)
        {
            for (int i = 0; i < 40; i++)
            {
                if (bytesReceived >= bytesCount)
                {
                    return;
                }

                Thread.Sleep(50);
            }

            throw new InvalidOperationException("Couldn't receive " + bytesCount + " bytes");
        }

        private void OnClosed(object o, EventArgs e)
        {
            closedEvent.Set();
        }

        private void OnDuplexChannelClosed(object o, ChannelCloseEventArgs e)
        {
            closeErrorData[e.DuplexSide] = e;
        }

        private void FillArray(byte[] array, int iteration)
        {
            for (int j = 0; j < array.Length; j++)
            {
                array[j] = (byte)(j + 1 + iteration);
            }
        }

        private byte[] GetData(int size, int iteration = 0)
        {
            byte[] data = new byte[size];
            FillArray(data, iteration);

            return data;
        }

        private void SendAndVerify(byte[] data)
        {
            wrapper.Send(data);

            WaitForData(data.Length);

            VerifyOtherEndData(otherEnd, data);
        }

        private void ReceiveAndVerify(byte[] data)
        {
            otherEnd.Send(data);

            WaitForReceivedData(data.Length);

            Assert.True(receivedData.ToArray().SequenceEqual(data));
        }

        private void SendAndVerifyByLength(int length)
        {
            SendAndVerify(GetData(length));
        }

        private void ReceiveAndVerifyByLength(int length)
        {
            ReceiveAndVerify(GetData(length));
        }

        private void SendPacketsAndVerify(int length)
        {
            MemoryStream ms = new MemoryStream();

            for (int i = 0; i < 200; i++)
            {
                byte[] data = GetData(length, i);

                wrapper.Send(data);
                ms.Write(data, 0, data.Length);
            }

            byte[] allData = ms.ToArray();

            WaitForData(allData.Length);

            VerifyOtherEndData(otherEnd, allData);
        }

        private void ReceivePacketsAndVerify(int length)
        {
            MemoryStream ms = new MemoryStream();

            for (int i = 0; i < 200; i++)
            {
                byte[] data = GetData(length, i);

                otherEnd.Send(data);
                ms.Write(data, 0, data.Length);
            }

            byte[] allData = ms.ToArray();

            WaitForReceivedData(allData.Length);

            Assert.True(receivedData.ToArray().SequenceEqual(allData));
        }

        [Fact]
        public void SendsSmallData()
        {
            SendAndVerifyByLength(5);
        }

        [Fact]
        public void SendsExactData()
        {
            SendAndVerifyByLength(11);
        }

        [Fact]
        public void SendsBigData()
        {
            SendAndVerifyByLength(512);
        }

        [Fact]
        public void SendsSmallPackets()
        {
            SendPacketsAndVerify(5);
        }

        [Fact]
        public void SendsExactPackets()
        {
            SendPacketsAndVerify(11);
        }

        [Fact]
        public void SendsBigPackets()
        {
            SendPacketsAndVerify(512);
        }

        [Fact]
        public void ReceivesSmallData()
        {
            ReceiveAndVerifyByLength(5);
        }

        [Fact]
        public void ReceivesExactData()
        {
            ReceiveAndVerifyByLength(17);
        }

        [Fact]
        public void ReceivesBigData()
        {
            ReceiveAndVerifyByLength(512);
        }

        [Fact]
        public void ReceivesSmallPackets()
        {
            ReceivePacketsAndVerify(5);
        }

        [Fact]
        public void ReceivesExactPackets()
        {
            ReceivePacketsAndVerify(17);
        }

        [Fact]
        public void ReceivesBigPackets()
        {
            ReceivePacketsAndVerify(512);
        }

        [Fact]
        public void ClosesFromOtherSide()
        {
            otherEnd.Close();

            Assert.True(closedEvent.Wait(TimeSpan.FromSeconds(2)));

            Assert.Equal(1, closeErrorData.Count);
            Assert.Equal(SocketError.ConnectionReset, closeErrorData[DuplexSide.Receive].SocketError);
        }

        [Fact]
        public void ClosesFromThisSide()
        {
            wrapper.Close();

            Assert.True(closedEvent.Wait(TimeSpan.FromSeconds(2)));

            Assert.Equal(1, closeErrorData.Count);
            
            Assert.True(SocketError.OperationAborted == closeErrorData[DuplexSide.Receive].SocketError ||
                        null == closeErrorData[DuplexSide.Receive].SocketError && null == closeErrorData[DuplexSide.Receive].Exception);
        }

        public override void Dispose()
        {
            wrapper.Close();

            base.Dispose();
        }
    }
}