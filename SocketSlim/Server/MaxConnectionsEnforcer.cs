using System.Threading;

namespace SocketSlim.Server
{
    public class MaxConnectionsEnforcer : IMaxConnectionsEnforcer
    {
        private readonly Semaphore semaphore;

        public MaxConnectionsEnforcer(int maxConnections)
        {
            semaphore = new Semaphore(maxConnections, maxConnections);
        }

        /// <summary>
        /// Blocks until the connection is available.
        /// </summary>
        public void TakeOne()
        {
            semaphore.WaitOne();
        }

        public void ReleaseOne()
        {
            try
            {
                semaphore.Release();
            }
            catch (SemaphoreFullException) { }
        }
    }
}