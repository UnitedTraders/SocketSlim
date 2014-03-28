using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SocketSlim.ChannelWrapper;
using SocketSlim.Client;

namespace SocketSlim
{
    public class ClientSocketSlim : IClientSocketSlim
    {
        private const int DefaultBufferSize = 8192;

        private IPAddress[] ipAddresses;
        private string host;

        private readonly AddressFamily? restrictedAddressFamily;

        private readonly TaskClientConnector connector;

        private readonly int receiveBufferSize;
        private readonly int sendBufferSize;

        private byte[] receiveBuffer;
        private byte[] sendBuffer;
        private SocketAsyncEventArgs receiver;
        private SocketAsyncEventArgs sender;
        private DirectBytesReceivedEventArgs receiverArgs;
        private MemoryStream senderWriter;
        private ChannelWrapperBase wrapper;

        private int state;

        public ClientSocketSlim(AddressFamily? restrictedAddressFamily = AddressFamily.InterNetwork)
            : this(false, restrictedAddressFamily)
        { }

        public ClientSocketSlim(bool preallocate, AddressFamily? restrictedAddressFamily = AddressFamily.InterNetwork)
            : this(DefaultBufferSize, preallocate, restrictedAddressFamily)
        { }

        public ClientSocketSlim(int bufferSize, bool preallocate, AddressFamily? restrictedAddressFamily = AddressFamily.InterNetwork)
            : this(bufferSize, bufferSize, preallocate, restrictedAddressFamily)
        { }

        public ClientSocketSlim(int receiveBufferSize, int sendBufferSize, bool preallocate, AddressFamily? restrictedAddressFamily = AddressFamily.InterNetwork)
        {
            this.receiveBufferSize = receiveBufferSize;
            this.sendBufferSize = sendBufferSize;
            this.restrictedAddressFamily = restrictedAddressFamily;

            if (preallocate)
            {
                AllocateCommunicationResources();
            }

            connector = new TaskClientConnector(SocketType.Stream, ProtocolType.Tcp, new SocketAsyncEventArgs());
            connector.Connected += OnConnectSucceeded;
            connector.Failed += OnConnectFailed;
        }

        protected virtual void OnConnectSucceeded(object o, SocketEventArgs e)
        {
            AllocateCommunicationResources();

            wrapper = new ChannelWrapperBase(e.Socket, receiver, receiverArgs, sender, senderWriter);
            wrapper.BytesReceived += OnBytesReceived;
            wrapper.Closed += OnChannelClosed;
            wrapper.DuplexChannelClosed += OnChannelError;
        }

        protected virtual void OnChannelError(object o, ChannelCloseEventArgs e)
        {
            if (e.Exception != null)
            {
                RaiseError(e.Exception);
            }
            else if (e.SocketError != null)
            {
                RaiseError(new SocketErrorException(e.SocketError.Value));
            }

            // when both are null, the socket is closed normally
        }

        protected virtual void OnChannelClosed(object o, EventArgs e)
        {
            wrapper = null;

            ChangeState(ChannelState.Disconnected);
        }

        protected virtual void OnBytesReceived(object o, BytesReceivedEventArgs e)
        {
            byte[] msg = new byte[e.Size];

            Buffer.BlockCopy(e.Buffer, e.Offset, msg, 0, e.Size);
            
            RaiseBytesReceived(msg);

            e.Proceed();
        }

        protected virtual void OnConnectFailed(object o, ExceptionEventArgs e)
        {
            RaiseError(e);

            ChangeState(ChannelState.Disconnected);
        }

        private void AllocateCommunicationResources()
        {
            if (receiveBuffer != null)
            {
                return;
            }

            receiveBuffer = new byte[receiveBufferSize];
            sendBuffer = new byte[sendBufferSize];

            receiver = new SocketAsyncEventArgs();
            sender = new SocketAsyncEventArgs();
            receiver.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
            sender.SetBuffer(sendBuffer, 0, sendBuffer.Length);

            receiverArgs = new DirectBytesReceivedEventArgs(receiver);
            senderWriter = new MemoryStream(sendBuffer, true);
        }

        public ChannelState State
        {
            get { return (ChannelState)state; }
        }

        public string Host
        {
            get { return host; }
            set { host = value; }
        }

        public int Port
        {
            get { return connector.Port; }
            set { connector.Port = value; }
        }

        private void ResolveHostName()
        {
            IPAddress ipFromString; // string with ip address is a special case.
            if (IPAddress.TryParse(host, out ipFromString))
            {
                ipAddresses = new[] { ipFromString };

                return;
            }

            ipAddresses = Dns.GetHostEntry(host).AddressList;

            if (restrictedAddressFamily != null) // filter ip addresses
            {
                ipAddresses = ipAddresses.Where(ip => ip.AddressFamily == restrictedAddressFamily.Value).ToArray();
            }
        }

        public void Open()
        {
            if (state != (int) ChannelState.Disconnected)
            {
                throw new InvalidOperationException("Can't open socket that's not diconnnected");
            }

            ChangeState(ChannelState.Connecting);

            try
            {

                ResolveHostName();

                connector.Address = ipAddresses[0]; // todo: select random IP

                connector.Connect();
            }
            catch
            {
                ChangeState(ChannelState.Disconnected);
            }
        }

        public void Close()
        {
            ChangeState(ChannelState.Disconnecting);

            if (wrapper != null)
            {
                wrapper.Close();
            }
            else
            {
                if (!connector.StopConnecting())
                {
                    ChangeState(ChannelState.Disconnected);
                }
            }
        }

        public void Send(byte[] bytes)
        {
            if (state != (int) ChannelState.Connected || wrapper == null)
            {
                throw new InvalidOperationException("Can't send data when the socket is not open");
            }

            wrapper.Send(bytes);
        }

        protected void ChangeState(ChannelState newState)
        {
            ChannelState oldState = (ChannelState)Interlocked.Exchange(ref state, (int)newState);

            RaiseStateChanged(new ChannelStateChangedEventArgs(oldState, newState));
        }

        public event EventHandler<ChannelStateChangedEventArgs> StateChanged;

        private void RaiseStateChanged(ChannelStateChangedEventArgs e)
        {
            EventHandler<ChannelStateChangedEventArgs> handler = StateChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event ClientSocketMessageHandler BytesReceived;

        protected void RaiseBytesReceived(byte[] message)
        {
            ClientSocketMessageHandler handler = BytesReceived;
            if (handler != null)
            {
                handler(this, message);
            }
        }

        public event EventHandler<ExceptionEventArgs> Error;

        protected void RaiseError(Exception e)
        {
            RaiseError(new ExceptionEventArgs(e));
        }

        protected void RaiseError(ExceptionEventArgs e)
        {
            EventHandler<ExceptionEventArgs> handler = Error;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
