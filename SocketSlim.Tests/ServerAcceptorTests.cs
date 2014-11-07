using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SocketSlim.Tests
{
    public class ServerAcceptorTests : ServerAcceptorTestsBase
    {
        [Fact]
        public void AcceptsOneClient()
        {
            StartAndCheckConnections(1);
        }

        [Fact]
        public void AcceptsManyCliens()
        {
            StartAndCheckConnections(Clients.Length);
        }

        private void StartAndCheckConnections(int connectionCount)
        {
            Acceptor.Start();

            for (int i = 0; i < connectionCount; i++)
            {
                StartClientConnect(i);
            }

            WaitForConnections(connectionCount);

            Assert.Equal(connectionCount, OpenedConnections.Count);
            Assert.True(OpenedConnections[0].Connected);

            WaitForServerConnections(connectionCount);
            Assert.Equal(connectionCount, ConnectionCount);

            Assert.Throws<InvalidOperationException>(() => WaitForErrors(1));
        }

        [Fact]
        public void RespectsBacklogSize()
        {
            Acceptor.MaxPendingConnections = 5;

            ManualResetEventSlim acceptorBarrier = new ManualResetEventSlim();
            AcceptorPool.Barrier = acceptorBarrier;
            lock (acceptorBarrier)
            {
                Task.Factory.StartNew(() => Acceptor.Start());

                Monitor.Wait(acceptorBarrier);
            }

            for (int i = 0; i < 9; i++)
            {
                StartClientConnect(i);
            }

            WaitForErrorsOnOtherSide(4); // first we get 4 rejects
            WaitForConnectionsOnOtherSide(5); // others are also immediately accepted

            Assert.Equal(4, Exceptions.Count);
            Assert.Equal(5, Connections.Count);

            Assert.Throws<InvalidOperationException>(() => WaitForErrorsOnOtherSide(5)); // nothing more happens
            Assert.Throws<InvalidOperationException>(() => WaitForConnectionsOnOtherSide(6));

            // release the lock and wait for other connections
            acceptorBarrier.Set();

            WaitForConnections(5);

            Assert.Equal(5, OpenedConnections.Count);
            Assert.True(OpenedConnections[0].Connected);

            WaitForServerConnections(5);
            Assert.Equal(5, ConnectionCount);

            // no more connections
            Assert.Throws<InvalidOperationException>(() => WaitForConnections(6));
            Assert.Throws<InvalidOperationException>(() => WaitForErrors(1)); // and we don't see errors from here
        }

        [Fact]
        public void RespectsMaxSimultaneousConnections()
        {
            Acceptor.MaxSimultaneousConnections = 5;
            Acceptor.Start();

            for (int i = 0; i < Clients.Length; i++)
            {
                StartClientConnect(i);
            }

            WaitForConnections(5);
            Assert.Throws<InvalidOperationException>(() => WaitForConnections(6)); // no more than 5 are connected
            Assert.Throws<InvalidOperationException>(() => WaitForErrors(1)); // no errors
            Assert.Throws<InvalidOperationException>(() => WaitForErrorsOnOtherSide(1)); // no errors on other side

            // simulate one of the connections closing
            Acceptor.ReleaseOpenConnectionSlot();

            WaitForConnections(6);
            Assert.Throws<InvalidOperationException>(() => WaitForConnections(7)); // no more than 5 are connected
            Assert.Throws<InvalidOperationException>(() => WaitForErrors(1)); // no errors
            Assert.Throws<InvalidOperationException>(() => WaitForErrorsOnOtherSide(1)); // no errors on other side
        }

        [Fact]
        public void OpenedChannelsSendData()
        {
            Acceptor.Start();
            for (int i = 0; i < Clients.Length; i++)
            {
                StartClientConnect(i);
            }

            byte[] testData = new byte[5000];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte) (i*i);
            }

            WaitForConnections(Clients.Length);
            foreach (Socket connection in OpenedConnections)
            {
                connection.Send(testData);
            }

            WaitForData(testData.Length * Clients.Length);

            foreach (Socket client in Clients)
            {
                VerifyOtherEndData(client, testData);
            }
        }

        [Fact]
        public void OpenedChannelsReceiveData()
        {
            Acceptor.Start();
            for (int i = 0; i < Clients.Length; i++)
            {
                StartClientConnect(i);
            }

            byte[] testData = new byte[5000];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)(i * i);
            }

            WaitForConnections(Clients.Length);
            foreach (Socket client in Clients)
            {
                client.Send(testData);
            }

            foreach (Socket socket in OpenedConnections)
            {
                byte[] rcvData = new byte[6000];
                int byteCount = 0;

                while ((byteCount += socket.Receive(rcvData, byteCount, rcvData.Length - byteCount, SocketFlags.None)) < testData.Length)
                {
                    // receive in loop
                }

                Assert.Equal(testData.Length, byteCount);
                Array.Resize(ref rcvData, byteCount);
                Assert.True(testData.SequenceEqual(rcvData));
            }
        }
    }
}