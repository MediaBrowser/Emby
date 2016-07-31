using MediaBrowser.Controller.Health;
using MediaBrowser.Controller.News;
using MediaBrowser.Model.News;
using MediaBrowser.Model.Querying;
using ServiceStack;

namespace MediaBrowser.Api
{
    [Route("/Health/Messages", "GET", Summary = "Gets the health messages.")]
    public class GetHealthMessages : IReturn<QueryResult<HealthMessage>>
    {
        /// <summary>
        /// Restricts to warnings and problems.
        /// </summary>
        /// <value>The warnings filter flag.</value>
        [ApiMember(Name = "WarningsOnly", Description = "Optional. Include only warnings and problems.", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "GET")]
        public bool? WarningsOnly { get; set; }
    }

    public class HealthService : BaseApiService
    {
        private readonly IHealthService _healthService;

        public HealthService(IHealthService healthService)
        {
            _healthService = healthService;
        }

        public object Get(GetHealthMessages request)
        {
            var result = _healthService.GetHealthMessages(new HealthQuery
            {
                WarningsOnly = request.WarningsOnly
            });

            return ToOptimizedSerializedResultUsingCache(result);
        }
    }
}
