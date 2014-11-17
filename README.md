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

Advanced
--------

Looking for something more flexible? In the library you'll also find building blocks which you can use and extend:

* `ChannelWrapperBase` is the utility class which takes care of sending and receiving data from a socket. It supports preallocated buffers, automatically queues up data for sending and provides some stats about sent and received data. There are some quirks in its contract, like you can't use the buffer after you've supplied it to the `Send` method, but those are needed to allow for maximum performance gains in various scenarios. Starting the receive loop requires calling `Start` to allow installing `BytesReceived` handler without a race condition.
* `ImmutableChannel` is a more sane wrapper for `ChannelWrapperBase` which takes the `Socket` and provides a duplex channel interface. Don't forget to call `Start` after creating it to start the receive loop.
* `ClientConnector` handles asynchronous connection procedure. You create it, subscribe to `Connected` and `Failed` events, set the `Address` and `Port` properties and call `Connect()`. You'll receive connection result (along with `Socket` if it was successful) through the events.
* `ServerAcceptor` handles asynchronous connection accepting. Like `ClientConnector`, you'll receive `Socket`s for the accepted channels through the events on this class. It supports pooling of the `SocketAsyncEventArgs` because setting up these objects is expensive.
