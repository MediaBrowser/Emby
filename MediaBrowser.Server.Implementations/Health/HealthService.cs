using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Health;
using MediaBrowser.Controller.Localization;
using MediaBrowser.Controller.News;
using MediaBrowser.Model.News;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MediaBrowser.Server.Implementations.Health
{
    public class HealthService : IHealthService
    {
        private readonly IApplicationPaths _appPaths;
        private readonly IJsonSerializer _json;
        private readonly IHealthReporter _healthReporter;
        private readonly ILocalizationManager _localizationManager;

        public HealthService(IApplicationPaths appPaths, IJsonSerializer json, IHealthReporter healthReporter, ILocalizationManager localizationManager)
        {
            _appPaths = appPaths;
            _json = json;
            _healthReporter = healthReporter;
            _localizationManager = localizationManager;
        }

        public QueryResult<HealthMessageLocalized> GetHealthMessages(HealthQuery query)
        {
            var messages = _healthReporter.GetAllHealthMessages().Result;


            if (query.WarningsOnly.HasValue && query.WarningsOnly.Value)
            {
                messages = messages.Where(e => e.Severity == HealthMessageSeverity.Warning || e.Severity == HealthMessageSeverity.Problem).ToList();
            }

            var localizedMessages = messages.OrderByDescending(o => (int)o.Severity).Select(e => e.ToLocalized(_localizationManager));

            return new QueryResult<HealthMessageLocalized>
            {
                Items = localizedMessages.ToArray(),
                TotalRecordCount = messages.Count
            };
        }
    }
}
