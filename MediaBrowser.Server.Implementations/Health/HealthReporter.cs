using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.FileOrganization;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.FileOrganization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using MediaBrowser.Controller.Health;
using System.Collections.Generic;

namespace MediaBrowser.Server.Implementations.FileOrganization
{
    public class HealthReporter : IHealthReporter
    {
        private readonly ILogger _logger;
        private readonly IServerConfigurationManager _config;
        private readonly IFileSystem _fileSystem;
        private readonly IServerManager _serverManager;
        private readonly List<HealthMessageBase> _messages = new List<HealthMessageBase>();

        public HealthReporter(ILogger logger, IServerConfigurationManager config, IFileSystem fileSystem, IServerManager serverManager)
        {
            _logger = logger;
            _config = config;
            _fileSystem = fileSystem;
            _serverManager = serverManager;
        }

        public void AddHealthMessage(HealthMessageBase healthMessage, bool replaceExistingById = true)
        {
            if (replaceExistingById)
            {
                RemoveHealthMessagesById(healthMessage.ReportingType, healthMessage.InfoGuid);
            }

            lock (_messages)
            {
                _messages.Add(healthMessage);
            }
        }

        public void RemoveHealthMessagesById(Type reportingType, Guid infoGuid)
        {
            lock (_messages)
            {
                var deleteMessages = _messages.Where(e => e.ReportingType.Equals(reportingType) && e.InfoGuid.Equals(infoGuid)).ToList();

                foreach (var message in deleteMessages)
                {
                    _messages.Remove(message);
                }
            }
        }

        public Task<System.Collections.Generic.List<HealthMessageBase>> GetAllHealthMessages()
        {
            throw new NotImplementedException();
        }
    }
}
