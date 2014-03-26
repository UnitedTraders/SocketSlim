using System;
using System.Net;
using System.Net.Sockets;

namespace SocketSlim.Client
{
    public class ClientConnector : IDisposable
    {
        private readonly SocketType socketType;
        private readonly ProtocolType protocolType;
        private readonly SocketAsyncEventArgs connector;

        private Socket socket;

        public ClientConnector(SocketType socketType, ProtocolType protocolType, SocketAsyncEventArgs connector)
        {
            this.socketType = socketType;
            this.protocolType = protocolType;
            this.connector = connector;

            connector.Completed += OnConnectCompleted;
        }

        public IPAddress Address { get; set; }

        public int Port { get; set; }

        public void Connect()
        {
            if (socket != null)
            {
                throw new InvalidOperationException("We're already connecting.");
            }

            socket = new Socket(Address.AddressFamily, socketType, protocolType)
                         {
                             NoDelay = true
                         };

            StartConnect();
        }

        public virtual bool StopConnecting()
        {
            Socket s = socket;
            if (s == null)
            {
                return false;
            }

            socket = null;

            try
            {
                s.Close();
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }

            return true;
        }

        private void StartConnect()
        {
            connector.RemoteEndPoint = new IPEndPoint(Address, Port);

            bool callbackPending;
            try
            {
                callbackPending = socket.ConnectAsync(connector);
            }
            catch (Exception e)
            {
                Fail(e);
                return;
            }

            if (!callbackPending)
            {
                ProcessConnect();
            }
        }

        private void OnConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessConnect();
        }

        private void ProcessConnect()
        {
            if (connector.SocketError != SocketError.Success)
            {
                Fail(new SocketErrorException(connector.SocketError));
                return;
            }

            Success(connector.ConnectSocket);
        }

        private void Success(Socket s)
        {
            socket = null;

            RaiseConnected(s);
        }

        private void Fail(Exception e)
        {
            socket = null;

            RaiseFailed(e);
        }

        public event EventHandler<SocketEventArgs> Connected;

        protected virtual void RaiseConnected(Socket s)
        {
            EventHandler<SocketEventArgs> handler = Connected;
            if (handler != null)
            {
                handler(this, new SocketEventArgs(s));
            }
        }

        public event EventHandler<ExceptionEventArgs> Failed;

        protected virtual void RaiseFailed(Exception e)
        {
            EventHandler<ExceptionEventArgs> handler = Failed;
            if (handler != null)
            {
                handler(this, new ExceptionEventArgs(e));
            }
        }

        public void Dispose()
        {
            connector.Completed -= OnConnectCompleted;
        }
    }
}