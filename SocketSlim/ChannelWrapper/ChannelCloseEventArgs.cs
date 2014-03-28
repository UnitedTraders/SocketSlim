using System;
using System.Net.Sockets;

namespace SocketSlim.ChannelWrapper
{
    public class ChannelCloseEventArgs : EventArgs
    {
        private readonly DuplexSide duplexSide;
        private readonly SocketError? socketError;
        private readonly Exception exception;

        public ChannelCloseEventArgs(DuplexSide duplexSide, SocketError? socketError, Exception exception)
        {
            this.duplexSide = duplexSide;
            this.socketError = socketError;
            this.exception = exception;
        }

        public DuplexSide DuplexSide
        {
            get { return duplexSide; }
        }

        public Exception Exception
        {
            get { return exception; }
        }

        public SocketError? SocketError
        {
            get { return socketError; }
        }
    }
}