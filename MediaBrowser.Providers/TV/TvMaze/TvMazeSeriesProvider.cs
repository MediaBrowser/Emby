using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.TV.TvMaze.Models;

namespace MediaBrowser.Providers.TV.TvMaze
{
    public class TvMazeSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        internal readonly SemaphoreSlim TvMazeResourcePool = new SemaphoreSlim(2, 2);
        internal static TvMazeSeriesProvider Current { get; private set; }
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _config;
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;

        public TvMazeSeriesProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager config, ILogger logger, ILibraryManager libraryManager)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _config = config;
            _logger = logger;
            _libraryManager = libraryManager;
            Current = this;
        }

        private const string SeriesSearchUrl = "http://api.tvmaze.com/search/shows?q={0}";
        private const string UrlSeriesData = "http://api.tvmaze.com/shows/{0}";
        private const string UrlSeriesEpisodes = "http://api.tvmaze.com/shows/{0}/episodes";
        private const string UrlSeriesSeasons = "http://api.tvmaze.com/shows/{0}/seasons";
        private const string UrlSeriesCast = "http://api.tvmaze.com/shows/{0}/cast";
        private const string UrlByRemoteId = "http://api.tvmaze.com/lookup/shows?{0}={1}";

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            if (IsValidSeries(searchInfo.ProviderIds))
            {
                var metadata = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);

                if (metadata.HasMetadata)
                {
                    return new List<RemoteSearchResult>
                    {
                        new RemoteSearchResult
                        {
                            Name = metadata.Item.Name,
                            PremiereDate = metadata.Item.PremiereDate,
                            ProductionYear = metadata.Item.ProductionYear,
                            ProviderIds = metadata.Item.ProviderIds,
                            SearchProviderName = Name
                        }
                    };
                }
            }

            return await FindSeries(searchInfo.Name, searchInfo.Year, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false);
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo itemId, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            if (!IsValidSeries(itemId.ProviderIds))
            {
                await Identify(itemId).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (IsValidSeries(itemId.ProviderIds))
            {
                await EnsureSeriesInfo(itemId.ProviderIds, itemId.MetadataLanguage, cancellationToken).ConfigureAwait(false);

                result.Item = new Series();
                result.HasMetadata = true;

                FetchSeriesData(result, itemId.MetadataLanguage, itemId.ProviderIds, cancellationToken);
            }

            return result;
        }

        /// <summary>
        /// Fetches the series data.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="metadataLanguage">The metadata language.</param>
        /// <param name="seriesProviderIds">The series provider ids.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.Boolean}.</returns>
        private void FetchSeriesData(MetadataResult<Series> result, string metadataLanguage, Dictionary<string, string> seriesProviderIds, CancellationToken cancellationToken)
        {
            var seriesDataPath = GetSeriesDataPath(_config.ApplicationPaths, seriesProviderIds);

            var seriesPath = GetSeriesPath(seriesDataPath);
            var castPath = GetCastPath(seriesDataPath);

            result.Item = FetchSeriesInfo(seriesPath, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var series = result.Item;

            string id;
            if (seriesProviderIds.TryGetValue(MetadataProviders.TvMaze.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                series.SetProviderId(MetadataProviders.TvMaze, id);
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.Tvdb.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                series.SetProviderId(MetadataProviders.Tvdb, id);
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.Imdb.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                series.SetProviderId(MetadataProviders.Imdb, id);
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.TvRage.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                series.SetProviderId(MetadataProviders.TvRage, id);
            }

            result.ResetPeople();

            FetchCast(result, castPath);
        }

        private async Task<string> GetSeriesByRemoteId(string id, MetadataProviders idType, CancellationToken cancellationToken)
        {
            var idTypeString = idType.ToString().ToLower();

            if (idTypeString == "tvdb")
            {
                idTypeString = "thetvdb";
            }

            var url = string.Format(UrlByRemoteId, idTypeString, id);

            using (var response = await _httpClient.GetResponse(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = TvMazeResourcePool,
                CancellationToken = cancellationToken

            }).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var arr = response.ResponseUrl.Split('/');
                    return arr[arr.Length - 1];
                }
            }

            return null;
        }

        internal static bool IsValidSeries(Dictionary<string, string> seriesProviderIds)
        {
            string id;
            if (seriesProviderIds.TryGetValue(MetadataProviders.TvMaze.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                return true;
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.Tvdb.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                return true;
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.Imdb.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                return true;
            }

            if (seriesProviderIds.TryGetValue(MetadataProviders.TvRage.ToString(), out id) && !string.IsNullOrEmpty(id))
            {
                return true;
            }

            return false;
        }

        private SemaphoreSlim _ensureSemaphore = new SemaphoreSlim(1, 1);
        internal async Task<string> EnsureSeriesInfo(Dictionary<string, string> seriesProviderIds, string preferredMetadataLanguage, CancellationToken cancellationToken)
        {
            await _ensureSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                string seriesId;
                MetadataProviders idType;

                if (seriesProviderIds.TryGetValue(MetadataProviders.TvMaze.ToString(), out seriesId) && !string.IsNullOrEmpty(seriesId))
                {
                    idType = MetadataProviders.TvMaze;
                }
                else if (seriesProviderIds.TryGetValue(MetadataProviders.Tvdb.ToString(), out seriesId) && !string.IsNullOrEmpty(seriesId))
                {
                    idType = MetadataProviders.Tvdb;
                }
                else if (seriesProviderIds.TryGetValue(MetadataProviders.TvRage.ToString(), out seriesId) && !string.IsNullOrEmpty(seriesId))
                {
                    idType = MetadataProviders.TvRage;
                }
                else if (seriesProviderIds.TryGetValue(MetadataProviders.Imdb.ToString(), out seriesId) && !string.IsNullOrEmpty(seriesId))
                {
                    idType = MetadataProviders.Imdb;
                }
                else
                {
                    throw new ArgumentException("TvMazeSeriesProvider.EnsureSeriesInfos: Missing provider id");
                }

                if (idType != MetadataProviders.TvMaze)
                {
                    seriesId = await GetSeriesByRemoteId(seriesId, idType, cancellationToken).ConfigureAwait(false);
                    seriesProviderIds[MetadataProviders.TvMaze.ToString()] = seriesId;
                }

                if (!string.IsNullOrWhiteSpace(seriesId))
                {
                    var seriesDataPath = GetSeriesDataPath(_config.ApplicationPaths, seriesProviderIds);

                    if (!IsCacheValid(seriesDataPath, preferredMetadataLanguage))
                    {
                        var url = string.Format(UrlSeriesData, seriesId);

                        using (var resultStream = await _httpClient.Get(new HttpRequestOptions
                        {
                            Url = url,
                            ResourcePool = TvMazeResourcePool,
                            CancellationToken = cancellationToken

                        }).ConfigureAwait(false))
                        {
                            if (!_fileSystem.DirectoryExists(seriesDataPath))
                            {
                                _fileSystem.CreateDirectory(seriesDataPath);
                            }

                            var mazeSeries = _jsonSerializer.DeserializeFromStream<MazeSeries>(resultStream);

                            if (mazeSeries.status == "404")
                            {
                                throw new Exception("TvMazeSeriesProvider: Series could not be found!");
                            }

                            // Delete existing files
                            DeleteCacheFiles(seriesDataPath);
                            
                            _jsonSerializer.SerializeToFile(mazeSeries, GetSeriesPath(seriesDataPath));
                        }

                        await DownloadEpisodes(seriesDataPath, seriesId, cancellationToken).ConfigureAwait(false);
                        await DownloadSeasons(seriesDataPath, seriesId, cancellationToken).ConfigureAwait(false);
                        await DownloadCast(seriesDataPath, seriesId, cancellationToken).ConfigureAwait(false);
                    }

                    return seriesDataPath;
                }

                return null;
            }
            finally
            {
                _ensureSemaphore.Release();
            }
        }

        private bool IsCacheValid(string seriesDataPath, string preferredMetadataLanguage)
        {
            try
            {
                var seriesFilename = GetSeriesPath(seriesDataPath);
                
                if (!_fileSystem.FileExists(seriesFilename))
                {
                    return false;
                }

                var fileInfo = _fileSystem.GetFileInfo(seriesFilename);

                const int cacheDays = 2;

                if (fileInfo == null || !fileInfo.Exists || (DateTime.UtcNow - fileInfo.LastWriteTimeUtc).TotalDays > cacheDays)
                {
                    return false;
                }

                return true;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the series.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="year">The year.</param>
        /// <param name="language">The language.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.String}.</returns>
        private async Task<IEnumerable<RemoteSearchResult>> FindSeries(string name, int? year, string language, CancellationToken cancellationToken)
        {
            var results = (await FindSeriesInternal(name, language, cancellationToken).ConfigureAwait(false)).ToList();

            if (results.Count == 0)
            {
                var parsedName = _libraryManager.ParseName(name);
                var nameWithoutYear = parsedName.Name;

                if (!string.IsNullOrWhiteSpace(nameWithoutYear) && !string.Equals(nameWithoutYear, name, StringComparison.OrdinalIgnoreCase))
                {
                    results = (await FindSeriesInternal(nameWithoutYear, language, cancellationToken).ConfigureAwait(false)).ToList();
                }
            }

            return results.Where(i =>
            {
                if (year.HasValue && i.ProductionYear.HasValue)
                {
                    // Allow one year tolerance
                    return Math.Abs(year.Value - i.ProductionYear.Value) <= 1;
                }

                return true;
            });
        }

        private async Task<IEnumerable<RemoteSearchResult>> FindSeriesInternal(string name, string language, CancellationToken cancellationToken)
        {
            var url = string.Format(SeriesSearchUrl, WebUtility.UrlEncode(name));
            MazeSearchContainerShow[] mazeResultItems;

            using (var results = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = TvMazeResourcePool,
                CancellationToken = cancellationToken

            }).ConfigureAwait(false))
            {
                mazeResultItems = _jsonSerializer.DeserializeFromStream<MazeSearchContainerShow[]>(results);
            }

            var searchResults = new List<RemoteSearchResult>();
            var comparableName = GetComparableName(name);

            foreach (var mazeResultItem in mazeResultItems)
            {
                var searchResult = new RemoteSearchResult
                {
                    SearchProviderName = Name
                };

                var mazeSeries = mazeResultItem.show;

                searchResult.Name = mazeSeries.name;
                searchResult.SetProviderId(MetadataProviders.TvMaze, mazeSeries.id.ToString());

                if (mazeSeries.externals != null && !string.IsNullOrWhiteSpace(mazeSeries.externals.imdb))
                {
                    searchResult.SetProviderId(MetadataProviders.Imdb, mazeSeries.externals.imdb);
                }

                if (mazeSeries.image != null && mazeSeries.image.original != null)
                {
                    searchResult.ImageUrl = mazeSeries.image.original.ToString();
                }

                if (mazeSeries.premiered.HasValue)
                {
                    searchResult.ProductionYear = mazeSeries.premiered.Value.Year;
                }

                searchResults.Add(searchResult);
            }

            if (searchResults.Count == 0)
            {
                _logger.Info("TV Maze Provider - Could not find " + name + ". Check name on tvmaze.com");
            }

            return searchResults;
        }

        /// <summary>
        /// The remove
        /// </summary>
        const string remove = "\"'!`?";
        /// <summary>
        /// The spacers
        /// </summary>
        const string spacers = "/,.:;\\(){}[]+-_=–*";  // (there are not actually two - in the they are different char codes)

        /// <summary>
        /// Gets the name of the comparable.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        internal static string GetComparableName(string name)
        {
            name = name.ToLower();
            name = name.Normalize(NormalizationForm.FormKD);
            var sb = new StringBuilder();
            foreach (var c in name)
            {
                if ((int)c >= 0x2B0 && (int)c <= 0x0333)
                {
                    // skip char modifier and diacritics 
                }
                else if (remove.IndexOf(c) > -1)
                {
                    // skip chars we are removing
                }
                else if (spacers.IndexOf(c) > -1)
                {
                    sb.Append(" ");
                }
                else if (c == '&')
                {
                    sb.Append(" and ");
                }
                else
                {
                    sb.Append(c);
                }
            }
            name = sb.ToString();
            name = name.Replace(", the", "");
            name = name.Replace("the ", " ");
            name = name.Replace(" the ", " ");

            string prevName;
            do
            {
                prevName = name;
                name = name.Replace("  ", " ");
            } while (name.Length != prevName.Length);

            return name.Trim();
        }

        private Series FetchSeriesInfo(string seriesJsonPath, CancellationToken cancellationToken)
        {
            var mazeSeries = _jsonSerializer.DeserializeFromFile<MazeSeries>(seriesJsonPath);
            return TvMazeAdapter.Convert(mazeSeries);
        }

        /// <summary>DS
        /// Fetches the actors.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="castJsonPath">The cast path.</param>
        private void FetchCast(MetadataResult<Series> result, string castJsonPath)
        {
            var persons = _jsonSerializer.DeserializeFromFile<PersonInfo[]>(castJsonPath);

            foreach (var person in persons)
            {
                result.AddPerson(person);
            }
        }

        /// <summary>
        /// Extracts info for each episode into invididual json files so that they can be easily accessed
        /// </summary>
        /// <param name="seriesDataPath">The series data path.</param>
        /// <param name="seriesId">The tvmaze id.</param>
        /// <param name="cancellationToken">The cancellationToken.</param>
        /// <returns>Task.</returns>
        private async Task DownloadEpisodes(string seriesDataPath, string seriesId, CancellationToken cancellationToken)
        {
            var url = string.Format(UrlSeriesEpisodes, seriesId);

            using (var resultStream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = TvMazeResourcePool,
                CancellationToken = cancellationToken

            }).ConfigureAwait(false))
            {
                var mazeEpisodes = _jsonSerializer.DeserializeFromStream<MazeEpisode[]>(resultStream);

                foreach (var mazeEpisode in mazeEpisodes)
                {
                    if (mazeEpisode.season.HasValue && mazeEpisode.number.HasValue)
                    {
                        var episodeFilename = GetEpisodePath(seriesDataPath, mazeEpisode.season.Value, mazeEpisode.number.Value);
                        _jsonSerializer.SerializeToFile(mazeEpisode, episodeFilename);
                    }
                }
            }
        }

        /// <summary>
        /// Extracts cast info for a series.
        /// </summary>
        /// <param name="seriesDataPath">The series data path.</param>
        /// <param name="seriesId">The tvmaze id.</param>
        /// <param name="cancellationToken">The cancellationToken.</param>
        /// <returns>Task.</returns>
        private async Task DownloadCast(string seriesDataPath, string seriesId, CancellationToken cancellationToken)
        {
            var url = string.Format(UrlSeriesCast, seriesId);

            using (var resultStream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = TvMazeResourcePool,
                CancellationToken = cancellationToken

            }).ConfigureAwait(false))
            {
                var mazeCastMembers = _jsonSerializer.DeserializeFromStream<MazeCastMember[]>(resultStream);

                var persons = new List<PersonInfo>();

                foreach (var mazeCastMember in mazeCastMembers)
                {
                    var person = TvMazeAdapter.Convert(mazeCastMember);
                    persons.Add(person);
                }

                var castPath = GetCastPath(seriesDataPath);
                _jsonSerializer.SerializeToFile(persons.ToArray(), castPath);
            }
        }

        /// <summary>
        /// Extracts info for each season into invididual json files so that they can be easily accessed
        /// </summary>
        /// <param name="seriesDataPath">The series data path.</param>
        /// <param name="seriesId">The tvmaze id.</param>
        /// <param name="cancellationToken">The cancellationToken.</param>
        /// <returns>Task.</returns>
        private async Task DownloadSeasons(string seriesDataPath, string seriesId, CancellationToken cancellationToken)
        {
            var url = string.Format(UrlSeriesSeasons, seriesId);

            using (var resultStream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = TvMazeResourcePool,
                CancellationToken = cancellationToken

            }).ConfigureAwait(false))
            {
                var mazeSeasons = _jsonSerializer.DeserializeFromStream<MazeSeason[]>(resultStream);

                foreach (var mazeSeason in mazeSeasons)
                {
                    if (mazeSeason.number.HasValue)
                    {
                        var seasonFilename = GetSeasonPath(seriesDataPath, mazeSeason.number.Value);
                        _jsonSerializer.SerializeToFile(mazeSeason, seasonFilename);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="seriesProviderIds">The series provider ids.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths, Dictionary<string, string> seriesProviderIds)
        {
            string seriesId;
            if (seriesProviderIds.TryGetValue(MetadataProviders.TvMaze.ToString(), out seriesId) && !string.IsNullOrEmpty(seriesId))
            {
                var dataPath = Path.Combine(appPaths.CachePath, "tvmaze");

                var seriesDataPath = Path.Combine(dataPath, seriesId);

                return seriesDataPath;
            }

            return null;
        }

        public string GetSeriesPath(string seriesDataPath)
        {
            var seriesFilename = "series.json";

            return Path.Combine(seriesDataPath, seriesFilename);
        }

        public string GetCastPath(string seriesDataPath)
        {
            var seriesFilename = "cast.json";

            return Path.Combine(seriesDataPath, seriesFilename);
        }

        public string GetEpisodePath(string seriesDataPath, int seasonNumber, int episodeNumber)
        {
            var episodeFilename = string.Format("episode-{0}-{1}.json", seasonNumber, episodeNumber);

            return Path.Combine(seriesDataPath, episodeFilename);
        }

        public string GetSeasonPath(string seriesDataPath, int seasonNumber)
        {
            var seasonFilename = string.Format("season-{0}.json", seasonNumber);

            return Path.Combine(seriesDataPath, seasonFilename);
        }

        private void DeleteCacheFiles(string path)
        {
            try
            {
                foreach (var file in _fileSystem.GetFilePaths(path, true).ToList())
                {
                    _fileSystem.DeleteFile(file);
                }
            }
            catch (DirectoryNotFoundException)
            {
                // No biggie
            }
        }

        public string Name
        {
            get { return "TV Maze"; }
        }

        public async Task Identify(SeriesInfo info)
        {
            if (!string.IsNullOrWhiteSpace(info.GetProviderId(MetadataProviders.TvMaze)))
            {
                return;
            }

            var srch = await FindSeries(info.Name, info.Year, info.MetadataLanguage, CancellationToken.None).ConfigureAwait(false);

            var entry = srch.FirstOrDefault();

            if (entry != null)
            {
                var id = entry.GetProviderId(MetadataProviders.TvMaze);
                info.SetProviderId(MetadataProviders.TvMaze, id);
            }
        }

        public int Order
        {
            get
            {
                // After Omdb or TvDB
                return 1;
            }
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                ResourcePool = TvMazeResourcePool
            });
        }
    }
}
