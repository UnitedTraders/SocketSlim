using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace SocketSlim.Server
{
    public class QueueSocketAsyncEventArgsPool : ISocketAsyncEventArgsPool
    {
        // Pool of reusable SocketAsyncEventArgs objects.
        private readonly ConcurrentQueue<SocketAsyncEventArgs> pool; // memory leaks of that queue are not of a great concern for us here

        // initializes the object pool to the specified size.
        public QueueSocketAsyncEventArgsPool()
        {
            pool = new ConcurrentQueue<SocketAsyncEventArgs>();
        }

        // The number of SocketAsyncEventArgs instances in the pool.
        public int Count
        {
            get { return pool.Count; }
        }

        // Removes a SocketAsyncEventArgs instance from the pool.
        // returns SocketAsyncEventArgs removed from the pool.
        public bool TryTake(out SocketAsyncEventArgs result)
        {
            return pool.TryDequeue(out result);
        }

        // Add a SocketAsyncEventArg instance to the pool.
        // "item" = SocketAsyncEventArgs instance to add to the pool.
        public void Put(SocketAsyncEventArgs item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item", "Item added to a QueueSocketAsyncEventArgsPool cannot be null");
            }

            pool.Enqueue(item);
        }
    }
}