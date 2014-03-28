using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SocketSlim.Client;
using System.Linq;

namespace SocketSlim.Tests
{
    public abstract class TestsBase : IDisposable
    {
        protected readonly ClientConnector Connector;
        protected static readonly IPAddress Addr = IPAddress.Parse("127.0.0.1");
        protected static readonly int Port = 32098;

        // server socket to test connection functionality
        private readonly Socket server;

        protected int ConnectionCount;
        protected readonly List<Socket> Connections = new List<Socket>();
        protected int BytesReceived;

        protected readonly List<Socket> OpenedConnections = new List<Socket>();
        protected readonly List<Exception> ConnectionErrors = new List<Exception>();

        protected readonly ConcurrentDictionary<Socket, MemoryStream> OtherEndData = new ConcurrentDictionary<Socket, MemoryStream>();

        protected TestsBase(ClientConnector connector = null)
        {
            // set up the connector
            Connector = connector ?? new ClientConnector(SocketType.Stream, ProtocolType.Tcp, new SocketAsyncEventArgs())
                                         {
                                             Address = Addr,
                                             Port = Port
                                         };
            Connector.Connected += OnConnected;
            Connector.Failed += OnFailed;

            // set up server socket
            server = new Socket(Addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(new IPEndPoint(Addr, Port));

            // start accepting connections
            server.Listen(10);
            server.BeginAccept(OnServerConnection, null);
        }

        private void OnConnected(object sender, SocketEventArgs e)
        {
            lock (OpenedConnections)
            {
                OpenedConnections.Add(e.Socket);

                Monitor.PulseAll(OpenedConnections);
            }
        }

        private void OnFailed(object sender, ExceptionEventArgs e)
        {
            lock (ConnectionErrors)
            {
                ConnectionErrors.Add(e.Exception);

                Monitor.PulseAll(ConnectionErrors);
            }
        }

        protected void WaitForConnections(int socketCount)
        {
            lock (OpenedConnections)
            {
                while (OpenedConnections.Count < socketCount)
                {
                    if (!Monitor.Wait(OpenedConnections, TimeSpan.FromSeconds(2)))
                    {
                        throw new InvalidOperationException("Couldn't get " + socketCount + " opened connection(s)");
                    }
                }
            }
        }

        protected void WaitForErrors(int errorCount)
        {
            lock (ConnectionErrors)
            {
                while (ConnectionErrors.Count < errorCount)
                {
                    if (!Monitor.Wait(ConnectionErrors, TimeSpan.FromSeconds(2)))
                    {
                        throw new InvalidOperationException("Couldn't get " + errorCount + " error(s) in time");
                    }
                }
            }
        }

        private class SocketReceiveState
        {
            private readonly Socket socket;
            private readonly byte[] buffer;

            public SocketReceiveState(Socket socket, byte[] buffer)
            {
                this.socket = socket;
                this.buffer = buffer;
            }

            public byte[] Buffer
            {
                get { return buffer; }
            }

            public Socket Socket
            {
                get { return socket; }
            }
        }

        private void OnServerConnection(IAsyncResult ar)
        {
            // get client socket
            Socket client;
            Connections.Add(client = server.EndAccept(ar));

            Interlocked.Increment(ref ConnectionCount);

            // start receiving data through it
            byte[] buffer = new byte[19]; // we test connections, we can get by with smaller buffers
            client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, OnClientData, new SocketReceiveState(client, buffer));
        }

        private void OnClientData(IAsyncResult ar)
        {
            SocketReceiveState state = (SocketReceiveState) ar.AsyncState;
            Socket client = state.Socket;

            int bytesRead;
            try
            {
                bytesRead = client.EndReceive(ar);
            }
            catch(ObjectDisposedException)
            {
                return;
            }

            if (bytesRead == 0)
            {
                client.Close();
                return;
            }

            OtherEndData.AddOrUpdate(client, s =>
                                                 {
                                                     MemoryStream ms = new MemoryStream();
                                                     ms.Write(state.Buffer, 0, bytesRead);
                                                     return ms;
                                                 },
                                             (s, ms) =>
                                                 {
                                                     ms.Write(state.Buffer, 0, bytesRead);
                                                     return ms;
                                                 });

            Interlocked.Add(ref BytesReceived, bytesRead);

            // receive again
            client.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, OnClientData, state);
        }

        protected void VerifyOtherEndData(Socket socket, byte[] data)
        {
            MemoryStream ms = OtherEndData[socket];

            if (!data.SequenceEqual(ms.ToArray()))
            {
                throw new InvalidOperationException("Other end data verification failed");
            }
        }

        protected void WaitForServerConnections(int connectionCnt)
        {
            for (int i = 0; i < 40; i++)
            {
                if (ConnectionCount >= connectionCnt)
                {
                    return;
                }

                Thread.Sleep(50);
            }

            throw new InvalidOperationException("Couldn't receive " + connectionCnt + " connections");
        }

        protected void WaitForData(int bytesCount, int cycles = 40)
        {
            for (int i = 0; i < cycles; i++)
            {
                if (BytesReceived >= bytesCount)
                {
                    return;
                }

                Thread.Sleep(50);
            }

            throw new InvalidOperationException("Couldn't receive " + bytesCount + " bytes");
        }

        public virtual void Dispose()
        {
            try
            {
                server.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException) { }

            server.Close();

            foreach (Socket connection in Connections)
            {
                connection.Dispose();
            }
            Connections.Clear();

            Connector.Dispose();
        }
    }
}