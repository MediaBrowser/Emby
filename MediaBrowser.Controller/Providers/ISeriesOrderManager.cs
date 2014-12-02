using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Providers
{
    public interface ISeriesOrderProvider
    {
        string OrderType { get; }
        Task<int?> FindSeriesIndex(string seriesName);
    }

    public static class SeriesOrderTypes
    {
        public const string Anime = "Anime";
    }

    public interface ISeriesOrderManager
    {
        Task<int?> FindSeriesIndex(string orderType, string seriesName);
        void AddParts(IEnumerable<ISeriesOrderProvider> orderProviders);
    }
}
