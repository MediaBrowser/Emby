using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CommonIO;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.TV.TvMaze.Models;

namespace MediaBrowser.Providers.TV.TvMaze
{
    public class TvMazeSeasonImageProvider : IRemoteImageProvider, IHasOrder
    {
        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");

        private readonly IServerConfigurationManager _config;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IJsonSerializer _jsonSerializer;

        public TvMazeSeasonImageProvider(IJsonSerializer jsonSerializer, IServerConfigurationManager config, IHttpClient httpClient, IFileSystem fileSystem)
        {
            _config = config;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _jsonSerializer = jsonSerializer;
        }

        public string Name
        {
            get { return ProviderName; }
        }

        public static string ProviderName
        {
            get { return TvMazeSeriesProvider.Current.Name; }
        }

        public bool Supports(IHasImages item)
        {
            return item is Season;
        }

        public IEnumerable<ImageType> GetSupportedImages(IHasImages item)
        {
            return new List<ImageType>
            {
                ImageType.Primary
            };
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(IHasImages item, CancellationToken cancellationToken)
        {
            var season = (Season)item;
            var series = season.Series;


            if (series != null && season.IndexNumber.HasValue && TvMazeSeriesProvider.IsValidSeries(series.ProviderIds))
            {
                var seriesProviderIds = series.ProviderIds;
                var seasonNumber = season.IndexNumber.Value;

                var seriesDataPath = await TvMazeSeriesProvider.Current.EnsureSeriesInfo(seriesProviderIds, series.GetPreferredMetadataLanguage(), cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(seriesDataPath))
                {
                    var seasonFileName = TvMazeSeriesProvider.Current.GetSeasonPath(seriesDataPath, seasonNumber);


                    try
                    {
                        var mazeSeason = _jsonSerializer.DeserializeFromFile<MazeSeason>(seasonFileName);
                        return GetImages(mazeSeason);
                    }
                    catch (FileNotFoundException)
                    {
                        // No tv maze data yet. Don't blow up
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // No tv maze data yet. Don't blow up
                    }
                }
            }

            return new RemoteImageInfo[] { };
        }

        private static IEnumerable<RemoteImageInfo> GetImages(MazeSeason mazeSeason)
        {
            var result = new List<RemoteImageInfo>();

            if (mazeSeason.image != null && mazeSeason.image.original != null)
            {
                var imageInfo = new RemoteImageInfo
                {
                    Url = mazeSeason.image.original.AbsoluteUri,
                    ProviderName = ProviderName,
                    Language = "en",
                    Type = ImageType.Primary
                };

                result.Add(imageInfo);
            }

            return result;
        }

        public int Order
        {
            get { return 0; }
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
