using System;
using System.Net.Sockets;

namespace SocketSlim.ChannelWrapper
{
    public class DirectBytesReceivedEventArgs : BytesReceivedEventArgs
    {
        private readonly SocketAsyncEventArgs receiver;
        private readonly ProceedReceiveEventArgs args = new ProceedReceiveEventArgs();

        public DirectBytesReceivedEventArgs(SocketAsyncEventArgs receiver)
        {
            this.receiver = receiver;
        }

        public override byte[] Buffer
        {
            get { return receiver.Buffer; }
        }

        public override int Offset
        {
            get { return receiver.Offset; }
        }

        public override int Size
        {
            get { return receiver.BytesTransferred; }
        }

        public override bool Proceed()
        {
            args.Closed = false;
            RaiseProceedReceive();
            return args.Closed;
        }

        public event EventHandler<ProceedReceiveEventArgs> ProceedReceive;

        public void RaiseProceedReceive()
        {
            EventHandler<ProceedReceiveEventArgs> handler = ProceedReceive;
            if (handler != null)
            {
                handler(this, args);
            }
        }
    }
}