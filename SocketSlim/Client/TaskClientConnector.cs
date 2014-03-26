using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SocketSlim.Client
{
    public class TaskClientConnector : ClientConnector
    {
        private TaskCompletionSource<Socket> taskCompletionSource;

        public TaskClientConnector(SocketType socketType, ProtocolType protocolType, SocketAsyncEventArgs connector)
            : base(socketType, protocolType, connector)
        {
        }

        public Task<Socket> ConnectAsync()
        {
            if (taskCompletionSource != null)
            {
                throw new InvalidOperationException("We're already connecting.");
            }

            TaskCompletionSource<Socket> newTaskSource = new TaskCompletionSource<Socket>();
            taskCompletionSource = newTaskSource;

            Connect();

            return newTaskSource.Task;
        }

        public override bool StopConnecting()
        {
            TaskCompletionSource<Socket> tcs = taskCompletionSource;
            if (tcs != null)
            {
                tcs.TrySetCanceled();
            }

            return base.StopConnecting();
        }

        protected override void RaiseConnected(Socket s)
        {
            taskCompletionSource.TrySetResult(s);
            taskCompletionSource = null;

            base.RaiseConnected(s);
        }

        protected override void RaiseFailed(Exception e)
        {
            taskCompletionSource.TrySetException(e);
            taskCompletionSource = null;

            base.RaiseFailed(e);
        }
    }
}