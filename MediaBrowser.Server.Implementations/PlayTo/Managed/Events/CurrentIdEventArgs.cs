using System;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Events
{
    public class CurrentIdEventArgs : EventArgs
    {
        public Guid Id
        { get; private set; }

        public CurrentIdEventArgs(string id)
        {
            if (id == null || id == "0")
                Id = Guid.Empty;
            else
                Id = Guid.Parse(id);
        }
    }
}
