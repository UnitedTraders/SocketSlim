using System;
using System.Net.Sockets;
using System.Threading;
using SocketSlim.Server;

namespace SocketSlim.Tests
{
    public class ServerAcceptorTestsBase : TestsBase
    {
        protected readonly DelayedAcceptorPool AcceptorPool = new DelayedAcceptorPool();
        protected readonly ServerAcceptor Acceptor;

        public ServerAcceptorTestsBase(int maxSimultaneousConnections = -1) : base(false)
        {
            Acceptor = new ServerAcceptor(SocketType.Stream, ProtocolType.Tcp, AcceptorPool, DelayedAcceptorPool.Factory)
            {
                ListenAddress = Addr,
                ListenPort = Port,
                MaxSimultaneousConnections = maxSimultaneousConnections
            };
            Acceptor.Accepted += OnConnected;
            Acceptor.AcceptFailed += OnFailed;
        }

        protected class DelayedAcceptorPool : ISocketAsyncEventArgsPool
        {
            private ManualResetEventSlim barrier;

            public static SocketAsyncEventArgs Factory()
            {
                return new SocketAsyncEventArgs();
            }

            public int Count
            {
                get { return Int32.MaxValue; }
            }

            public ManualResetEventSlim Barrier
            {
                get { return barrier; }
                set { barrier = value; }
            }

            public bool TryTake(out SocketAsyncEventArgs result)
            {
                if (barrier != null)
                {
                    lock (barrier)
                    {
                        // tell everyone we're at this place in code
                        Monitor.PulseAll(barrier);
                    }

                    barrier.Wait();
                }

                result = Factory();

                return true;
            }

            public void Put(SocketAsyncEventArgs item)
            {
                // do nothing
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Acceptor.Stop();
        }
    }
}