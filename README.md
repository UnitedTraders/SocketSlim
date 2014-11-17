SocketSlim
==========

.NET Socket wrapper aimed both at ease of use and performance.

For any user, the socket is a duplex stream, you send bytes and you receive bytes, but to do it efficiently in .NET requires you to manually fill in buffers, manage queues, callbacks and states. SocketSlim provides you with the easy interface that hides the complexity but allows a great deal of performance tuning.

Connecting and accepting connections is as easy as subscribing to the `Connected` event and calling `Start()`. Then you can send and receive data by simply subscribing to `BytesReceived` event and calling `Send(bytes)` method on a given channel object.

Usage
-----

```csharp
// create socket and set it up
var server = new ServerSocketSlim(sendReceiveBufferSize: 8192)
{
    ListenAddress = IPAddress.Parse("::"),
    ListenPort = 12312
};

// subscribe to relevant events
server.Connected += (s, args) =>
{
    ISocketChannel channel = args.Channel;
    
    channel.Closed += OnChannelClosed;
    channel.Error += OnChannelError;
    
    channel.BytesReceived += OnChannelBytesReceived;
    channel.Send(new byte[] { 1, 2, 3 });
};

// start socket
server.Start();
```
