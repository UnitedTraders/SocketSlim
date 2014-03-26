using System;
using System.Threading;

namespace SocketSlim
{
    public class ClientSocketSlimBase
    {
        private int state;

        public ChannelState State
        {
            get { return (ChannelState)state; }
        }

        private void ChangeState(ChannelState newState)
        {
            ChannelState oldState = (ChannelState)Interlocked.Exchange(ref state, (int)newState);

            RaiseStateChanged(new ChannelStateChangedEventArgs(oldState, newState));
        }

        public event EventHandler<ChannelStateChangedEventArgs> StateChanged;

        protected virtual void RaiseStateChanged(ChannelStateChangedEventArgs e)
        {
            EventHandler<ChannelStateChangedEventArgs> handler = StateChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
