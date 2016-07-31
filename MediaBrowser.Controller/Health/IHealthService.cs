using MediaBrowser.Model.News;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Controller.Health
{
    /// <summary>
    /// Interface IHealthService
    /// </summary>
    public interface IHealthService
    {
        /// <summary>
        /// Gets the health messages.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>QueryResult{HealthMessageLocalized}.</returns>
        QueryResult<HealthMessageLocalized> GetHealthMessages(HealthQuery query);
    }
}
