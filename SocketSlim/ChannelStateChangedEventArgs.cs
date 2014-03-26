using System;

namespace SocketSlim
{
    public class ChannelStateChangedEventArgs : EventArgs
    {
        private readonly ChannelState oldState;
        private readonly ChannelState newState;

        public ChannelStateChangedEventArgs(ChannelState oldState, ChannelState newState)
        {
            this.oldState = oldState;
            this.newState = newState;
        }

        public ChannelState NewState
        {
            get { return newState; }
        }

        public ChannelState OldState
        {
            get { return oldState; }
        }
    }
}