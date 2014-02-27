using System;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Events
{
    public class TransportStateEventArgs : EventArgs
    {
        public bool Stopped
        { get; private set; }

        public TransportStateEventArgs(bool stopped)
        {
            Stopped = stopped;
        }
    }

    
}
