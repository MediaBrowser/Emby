using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Querying;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Health
{
    public enum HealthMessageVerdict
    {
        Informational = 0,
        ConfirmedOK = 1,
        Suggestion = 2,
        Warning = 3,
        Problem = 4
    }
}
