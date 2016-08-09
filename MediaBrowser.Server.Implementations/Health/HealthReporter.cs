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
using MediaBrowser.Controller.Localization;

namespace MediaBrowser.Server.Implementations.Health
{
    public class HealthReporter : IHealthReporter
    {
        private readonly ILogger _logger;
        private readonly ILocalizationManager _localizationManager;
        private readonly List<HealthMessage> _messages = new List<HealthMessage>();

        public HealthReporter(ILogger logger, ILocalizationManager localizationManager)
        {
            _logger = logger;
            _localizationManager = localizationManager;
        }

        public void AddHealthMessage(HealthMessage healthMessage, bool replaceExistingById = true)
        {
            if (replaceExistingById)
            {
                RemoveHealthMessagesById(healthMessage.ReportingType, healthMessage.MessageId);
            }

            lock (_messages)
            {
                _messages.Add(healthMessage);
            }
        }

        public void RemoveHealthMessagesById(object reporter, string messageId)
        {
            this.RemoveHealthMessagesById(reporter.GetType(), messageId);
        }

        public void AddRemoveHealthMessage(bool condition, HealthMessage healthMessage)
        {
            if (condition)
            {
                this.AddHealthMessage(healthMessage, true);
            }
            else
            {
                this.RemoveHealthMessagesById(healthMessage.ReportingType, healthMessage.MessageId);
            }
        }

        public Task<System.Collections.Generic.List<HealthMessage>> GetAllHealthMessages()
        {
            lock (_messages)
            {
                return Task.FromResult(_messages.ToList());
            }
        }

        private void RemoveHealthMessagesById(Type reportingType, string messageId)
        {
            lock (_messages)
            {
                var deleteMessages = _messages.Where(e => e.ReportingType.Equals(reportingType) && e.MessageId.Equals(messageId)).ToList();

                foreach (var message in deleteMessages)
                {
                    _messages.Remove(message);
                }
            }
        }
    }
}
