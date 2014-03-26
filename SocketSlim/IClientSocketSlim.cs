using System;

namespace SocketSlim
{
    public interface IClientSocketSlim
    {
        // TODO: OPEN AND CLOSE WHICH RETURN TASKS!!!

        string Host { get; set; }
        int Port { get; set; }

        ChannelState State { get; }
        event EventHandler<ChannelStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Starts connecting to the specified <see cref="Host"/> and <see cref="Port"/>.
        /// 
        /// Throws exception if already connected or connecting, check the <see cref="State"/>.
        /// </summary>
        void Open();
        
        /// <summary>
        /// Closes the current connection. If we're connecting, interrupts the connection process.
        /// 
        /// Throws exception if we're disconnected.
        /// </summary>
        void Close();

        /// <summary>
        /// Sends the array of bytes into the socket. Can be called from multiple threads simultaneously.
        /// </summary>
        void Send(byte[] msg);

        event EventHandler<BytesReceivedEventArgs> BytesReceived;
    }

    public class BytesReceivedEventArgs : EventArgs
    {
        private readonly byte[] bytes;

        public BytesReceivedEventArgs(byte[] bytes)
        {
            this.bytes = bytes;
        }

        public byte[] Bytes
        {
            get { return bytes; }
        }
    }
}