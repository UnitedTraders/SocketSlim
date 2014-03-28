using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SocketSlim.Client;
using Xunit;

namespace SocketSlim.Tests
{
    public class TaskClientConnectorTests : ClientConnectorTests
    {
        private readonly TaskClientConnector connector;

        public TaskClientConnectorTests()
            : this(new TaskClientConnector(SocketType.Stream, ProtocolType.Tcp, new SocketAsyncEventArgs())
                       {
                           Address = Addr,
                           Port = Port
                       })
        { }

        public TaskClientConnectorTests(TaskClientConnector connector) : base(connector)
        {
            this.connector = connector;
        }

        [Fact]
        public void AcceptsThruTask()
        {
            Task<Socket> task = connector.ConnectAsync();

            Assert.True(task.Wait(TimeSpan.FromSeconds(2)));

            Assert.NotNull(task.Result);

            Assert.Equal(1, OpenedConnections.Count);
            Assert.True(OpenedConnections[0].Connected);

            WaitForServerConnections(1);
            Assert.Equal(1, ConnectionCount);

            Assert.Throws<InvalidOperationException>(() => WaitForErrors(1));
        }

        [Fact]
        public void CancelsTask()
        {
            Connector.Address = IPAddress.Parse("8.8.8.8");
            Connector.Port = Port + 1;

            Task<Socket> task = connector.ConnectAsync();
            Connector.StopConnecting();

            AggregateException ex = Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(2)));
            Assert.IsType<TaskCanceledException>(ex.InnerException);

            WaitForErrors(1);

            Assert.Equal(1, ConnectionErrors.Count);
            SocketErrorException ex2 = (SocketErrorException)ConnectionErrors[0];
            Assert.Equal(SocketError.OperationAborted, ex2.SocketError);

            Assert.Throws<InvalidOperationException>(() => WaitForConnections(1));
        }

        [Fact]
        public void ReportsProblemViaTask()
        {
            Connector.Port = Port + 1;

            Task<Socket> task = connector.ConnectAsync();

            AggregateException ex = Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(2)));
            Assert.IsType<SocketErrorException>(ex.InnerException);

            WaitForErrors(1);

            Assert.Equal(1, ConnectionErrors.Count);
            SocketErrorException ex2 = (SocketErrorException)ConnectionErrors[0];
            Assert.Equal(SocketError.ConnectionRefused, ex2.SocketError);

            Assert.Throws<InvalidOperationException>(() => WaitForConnections(1));
        }
    }
}