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
    public enum HealthMessageType
    {
        General = 0,
        Configuration = 1,
        ServerStatus = 2,
        Custom = 999
    }
}
