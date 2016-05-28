using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CommonIO;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.TV.TvMaze.Models;

namespace MediaBrowser.Providers.TV.TvMaze
{

    /// <summary>
    /// Class TvMazeSeasonProvider
    /// </summary>
    class TvMazeSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>, IHasItemChangeMonitor
    {
        internal static TvMazeSeasonProvider Current;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _config;
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;

        public TvMazeSeasonProvider(IJsonSerializer jsonSerializer, IFileSystem fileSystem, IServerConfigurationManager config, IHttpClient httpClient, ILogger logger)
        {
            _jsonSerializer = jsonSerializer;
            _fileSystem = fileSystem;
            _config = config;
            _httpClient = httpClient;
            _logger = logger;
            Current = this;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(new List<RemoteSearchResult>());
        }

        public string Name
        {
            get { return TvMazeSeriesProvider.Current.Name; }
        }

        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>();

            if (TvMazeSeriesProvider.IsValidSeries(searchInfo.SeriesProviderIds) && 
				searchInfo.IndexNumber.HasValue)
            {
                result.QueriedById = true;
                var seriesDataPath = await TvMazeSeriesProvider.Current.EnsureSeriesInfo(searchInfo.SeriesProviderIds, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false);

                try
                {
                    result = FetchSeasonData(searchInfo, seriesDataPath, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    // Don't fail the provider because this will just keep on going and going.
                }
                catch (DirectoryNotFoundException)
                {
                    // Don't fail the provider because this will just keep on going and going.
                }
            }
            else
            {
                _logger.Debug("No series identity found for {0}", searchInfo.Name);
            }

            return result;
        }

        public bool HasChanged(IHasMetadata item, IDirectoryService directoryService)
        {
            // Only enable for virtual items
            if (item.LocationType != LocationType.Virtual)
            {
                return false;
            }

            var episode = (Episode)item;
            var series = episode.Series;

            if (series != null && TvMazeSeriesProvider.IsValidSeries(series.ProviderIds))
            {
                // Process images
                var seriesDataPath = TvMazeSeriesProvider.GetSeriesDataPath(_config.ApplicationPaths, series.ProviderIds);
                var seriesPath = TvMazeSeriesProvider.Current.GetSeriesPath(seriesDataPath);

                return _fileSystem.GetLastWriteTimeUtc(seriesPath) > item.DateLastRefreshed;
            }

            return false;
        }

        /// <summary>
        /// Fetches the season data.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="seriesDataPath">The series data path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Season}.</returns>
        private MetadataResult<Season> FetchSeasonData(SeasonInfo info, string seriesDataPath, CancellationToken cancellationToken)
        {
            var seasonFileName = TvMazeSeriesProvider.Current.GetSeasonPath(seriesDataPath, info.IndexNumber.Value);

            var mazeSeason = _jsonSerializer.DeserializeFromFile<MazeSeason>(seasonFileName);
            var season = TvMazeAdapter.Convert(mazeSeason);

            if (string.IsNullOrEmpty(season.Name))
            {
                season.Name = info.Name;
            }

            if (!season.IndexNumber.HasValue)
            {
                season.IndexNumber = info.IndexNumber.Value;
            }

            var result = new MetadataResult<Season>()
			{
                Item = season,
                HasMetadata = true
			};

            return result;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                ResourcePool = TvMazeSeriesProvider.Current.TvMazeResourcePool
            });
        }
    }
}
