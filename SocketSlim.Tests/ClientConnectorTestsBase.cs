using System.Net.Sockets;
using SocketSlim.Client;

namespace SocketSlim.Tests
{
    public abstract class ClientConnectorTestsBase : TestsBase
    {
        protected readonly ClientConnector Connector;

        protected ClientConnectorTestsBase(ClientConnector connector = null)
            : base(dummyServer: true)
        {
            Connector = connector ?? new ClientConnector(SocketType.Stream, ProtocolType.Tcp, new SocketAsyncEventArgs())
            {
                Address = Addr,
                Port = Port
            };
            Connector.Connected += OnConnected;
            Connector.Failed += OnFailed;
        }

        public override void Dispose()
        {
            base.Dispose();

            Connector.Dispose();
        }
    }
}