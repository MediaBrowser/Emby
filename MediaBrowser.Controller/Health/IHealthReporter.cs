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
        ILocalizationManager LocalizationManager { get; }

        void AddHealthMessage(HealthMessageBase healthMessage, bool replaceExistingById = true);

        void RemoveHealthMessagesById(Type reportingType, Guid infoGuid);

        Task<List<HealthMessageBase>> GetAllHealthMessages();
    }
}
