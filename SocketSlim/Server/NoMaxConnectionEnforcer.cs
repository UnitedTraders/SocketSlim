namespace SocketSlim.Server
{
    public class NoMaxConnectionEnforcer : IMaxConnectionsEnforcer
    {
        public void TakeOne() { }

        public void ReleaseOne() { }
    }
}