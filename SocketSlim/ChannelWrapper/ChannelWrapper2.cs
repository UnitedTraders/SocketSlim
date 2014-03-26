using System.IO;
using System.Net.Sockets;

namespace SocketSlim.ChannelWrapper
{
    // todo: remove this, it's only an example of how not to do stuff
    public class ChannelWrapper2 : ChannelWrapperBase
    {
        public ChannelWrapper2(Socket socket, SocketAsyncEventArgs receiver, SocketAsyncEventArgs sender)
            : base(socket, receiver, new DirectBytesReceivedEventArgs(receiver), sender, new MemoryStream(sender.Buffer, sender.Offset, sender.Count, true))
        {
        }
    }
}