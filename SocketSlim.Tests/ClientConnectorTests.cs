using System;
using System.Net;
using System.Net.Sockets;
using SocketSlim.Client;
using Xunit;

namespace SocketSlim.Tests
{
    public class ClientConnectorTests : ClientConnectorTestsBase
    {
        public ClientConnectorTests() : this(null)
        { }

        internal ClientConnectorTests(ClientConnector connector)
            : base(connector)
        { }

        [Fact]
        public void DisposesFine()
        {
            // just run the .ctor->dispose cycle
        }

        [Fact]
        public void ConnectsSuccessfully()
        {
            Connector.Connect();

            WaitForConnections(1);

            Assert.Equal(1, OpenedConnections.Count);
            Assert.True(OpenedConnections[0].Connected);
            
            WaitForServerConnections(1);
            Assert.Equal(1, ConnectionCount);

            Assert.Throws<InvalidOperationException>(() => WaitForErrors(1));
        }

        [Fact]
        public void CanSendData()
        {
            Connector.Connect();

            WaitForConnections(1);

            byte[] data = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12};

            Socket opened = OpenedConnections[0];
            opened.Send(data);

            WaitForData(12);

            Assert.Equal(12, BytesReceived);

            VerifyOtherEndData(Connections[0], data);
        }

        [Fact]
        public void ReportsCancellation()
        {
            Connector.Address = IPAddress.Parse("8.8.8.8");
            Connector.Port = Port + 1;

            Connector.Connect();
            Connector.StopConnecting();

            WaitForErrors(1);

            Assert.Equal(1, ConnectionErrors.Count);
            SocketErrorException ex = (SocketErrorException) ConnectionErrors[0];
            Assert.Equal(SocketError.OperationAborted, ex.SocketError);
            
            Assert.Throws<InvalidOperationException>(() => WaitForConnections(1));
        }

        [Fact]
        public void ReportsUnavailability()
        {
            Connector.Port = Port + 1;

            Connector.Connect();

            WaitForErrors(1);

            Assert.Equal(1, ConnectionErrors.Count);
            SocketErrorException ex = (SocketErrorException)ConnectionErrors[0];
            Assert.Equal(SocketError.ConnectionRefused, ex.SocketError);

            Assert.Throws<InvalidOperationException>(() => WaitForConnections(1));
        }
    }
}