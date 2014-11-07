using System;
using System.Net;
using SocketSlim.ChannelWrapper;
using SocketSlim.Client;

namespace SocketSlim
{
    public interface IServerSocketSlim
    {
        ServerState State { get; }
        IPAddress ListenAddress { get; set; }
        int ListenPort { get; set; }
        void Start();
        void Stop();
        event EventHandler<ServerStateChangedEventArgs> StateChanged;
        event EventHandler<ExceptionEventArgs> Error;
        event EventHandler<ChannelEventArgs> Connected;
    }
}