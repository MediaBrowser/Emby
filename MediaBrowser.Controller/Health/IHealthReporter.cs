using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Notifications;
using System;
using MediaBrowser.Controller.Localization;

namespace MediaBrowser.Controller.Health
{
    public interface IHealthReporter
    {
        void AddHealthMessage(HealthMessage healthMessage, bool replaceExistingById = true);

        void RemoveHealthMessagesById(object reporter, string messageId);

        void AddRemoveHealthMessage(bool condition, HealthMessage healthMessage);

        Task<List<HealthMessage>> GetAllHealthMessages();
    }
}
