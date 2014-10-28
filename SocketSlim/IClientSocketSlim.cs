using System;
using SocketSlim.ChannelWrapper;
using SocketSlim.Client;

namespace SocketSlim
{
    public interface IClientSocketSlim
    {
        /// <summary>
        /// Gets the current state of the socket.
        /// </summary>
        ChannelState State { get; }

        /// <summary>
        /// Gets or sets the hostname to which the socket is/will be connecting.
        /// </summary>
        string Host { get; set; }

        /// <summary>
        /// Gets or sets the port to which the socket is/will be connecting.
        /// </summary>
        int Port { get; set; }

        /// <summary>
        /// Starts connecting to the specified <see cref="Host"/> and <see cref="Port"/>.
        /// 
        /// It's an asynchronous operation, you should start sending data only
        /// after you receive <see cref="StateChanged"/> event with the <see cref="ChannelState.Connected"/>.
        /// 
        /// If anything fails while connecting, you will receive <see cref="StateChanged"/> event with the <see cref="ChannelState.Disconnected"/>.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops connecting.
        /// 
        /// It's an asynchronous operation, you should start connecting again only
        /// after you receive <see cref="StateChanged"/> event with the <see cref="ChannelState.Disconnected"/>.
        /// 
        /// To close the opened connection, use <see cref="ISocketChannel.Close"/> method on the object you've got through <see cref="Connected"/> event.
        /// </summary>
        void Stop();

        /// <summary>
        /// Raised when socket changes state. Use this event to monitor when socket is ready, or aborted the connection.
        /// </summary>
        event EventHandler<ChannelStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Raised when any error occurs, whether it was during the connection or sending/receiving data.
        /// </summary>
        event EventHandler<ExceptionEventArgs> Error;

        /// <summary>
        /// Raised when the socket is closed successfully. Contains the object that should be used to send and receive data.
        /// </summary>
        event EventHandler<ChannelEventArgs> Connected;
    }
}