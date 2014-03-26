using System;
using System.Net.Sockets;

namespace SocketSlim
{
    public class SocketErrorException : Exception
    {
        private readonly SocketError socketError;

        public SocketErrorException(SocketError socketError)
            : base("Socket operation failed with the " + socketError + " error")
        {
            this.socketError = socketError;
        }

        public SocketError SocketError1
        {
            get { return socketError; }
        }
    }
}