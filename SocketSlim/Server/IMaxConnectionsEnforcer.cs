namespace SocketSlim.Server
{
    public interface IMaxConnectionsEnforcer
    {
        /// <summary>
        /// Blocks until the connection is available.
        /// </summary>
        void TakeOne();

        void ReleaseOne();
    }
}