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
    public enum HealthMessageSeverity
    {
        Informational = 0,
        Suggestion = 1,
        Warning = 2,
        Problem = 3
    }
}
