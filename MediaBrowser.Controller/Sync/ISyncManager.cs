using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Sync;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Sync
{
    public interface ISyncManager
    {
        /// <summary>
        /// Supportses the synchronize.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool SupportsSync(BaseItem item);
    }
}
