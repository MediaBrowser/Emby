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
    /// Class RemoteEpisodeProvider
    /// </summary>
    class TvMazeEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasItemChangeMonitor
    {
        internal static TvMazeEpisodeProvider Current;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _config;
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;

        public TvMazeEpisodeProvider(IJsonSerializer jsonSerializer, IFileSystem fileSystem, IServerConfigurationManager config, IHttpClient httpClient, ILogger logger)
        {
            _jsonSerializer = jsonSerializer;
            _fileSystem = fileSystem;
            _config = config;
            _httpClient = httpClient;
            _logger = logger;
            Current = this;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();

			// The search query must  provide an episode number
			if (!searchInfo.IndexNumber.HasValue) 
			{
				return list;
			}

            if (TvMazeSeriesProvider.IsValidSeries(searchInfo.SeriesProviderIds))
            {
                var seriesDataPath = TvMazeSeriesProvider.GetSeriesDataPath(_config.ApplicationPaths, searchInfo.SeriesProviderIds);

                try
                {
                    var metadataResult = FetchEpisodeData(searchInfo, seriesDataPath, cancellationToken);

                    if (metadataResult.HasMetadata)
                    {
                        var item = metadataResult.Item;

                        list.Add(new RemoteSearchResult
                        {
                            IndexNumber = item.IndexNumber,
                            Name = item.Name,
                            ParentIndexNumber = item.ParentIndexNumber,
                            PremiereDate = item.PremiereDate,
                            ProductionYear = item.ProductionYear,
                            ProviderIds = item.ProviderIds,
                            SearchProviderName = Name,
                            IndexNumberEnd = item.IndexNumberEnd
                        });
                    }
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

            return list;
        }

        public string Name
        {
            get { return TvMazeSeriesProvider.Current.Name; }
        }

        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            if (TvMazeSeriesProvider.IsValidSeries(searchInfo.SeriesProviderIds) && 
				(searchInfo.IndexNumber.HasValue || searchInfo.PremiereDate.HasValue))
            {
                result.QueriedById = true;

                var seriesDataPath = await TvMazeSeriesProvider.Current.EnsureSeriesInfo(searchInfo.SeriesProviderIds, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false);

                try
                {
                    result = FetchEpisodeData(searchInfo, seriesDataPath, cancellationToken);
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
        /// Fetches the episode data.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="seriesDataPath">The series data path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Episode}.</returns>
        private MetadataResult<Episode> FetchEpisodeData(EpisodeInfo id, string seriesDataPath, CancellationToken cancellationToken)
        {
            var episodeFileName = TvMazeSeriesProvider.Current.GetEpisodePath(seriesDataPath, id.ParentIndexNumber.Value, id.IndexNumber.Value);

            var mazeEpisode = _jsonSerializer.DeserializeFromFile<MazeEpisode>(episodeFileName);
            var episode = TvMazeAdapter.Convert(mazeEpisode);

			var result = new MetadataResult<Episode>()
			{
                Item = episode,
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

        public int Order { get { return 0; } }
    }
}
