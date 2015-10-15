using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.IO;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.FileOrganization;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.FileOrganization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using System.Collections.Concurrent;
using MediaBrowser.Controller.Localization;

namespace MediaBrowser.Server.Implementations.FileOrganization
{
    public class FileOrganizationService : IFileOrganizationService
    {
        private readonly ITaskManager _taskManager;
        private readonly IFileOrganizationRepository _repo;
        private readonly ILogger _logger;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly IServerConfigurationManager _config;
        private readonly IFileSystem _fileSystem;
        private readonly IProviderManager _providerManager;
        private readonly IServerManager _serverManager;
        private readonly ILocalizationManager _localizationManager;
        private readonly ConcurrentDictionary<string, bool> _inProgressItemIds = new ConcurrentDictionary<string, bool>();

        public FileOrganizationService(ITaskManager taskManager, IFileOrganizationRepository repo, ILogger logger, ILibraryMonitor libraryMonitor, ILibraryManager libraryManager, IServerConfigurationManager config, IFileSystem fileSystem, IProviderManager providerManager, IServerManager serverManager, ILocalizationManager localizationManager)
        {
            _taskManager = taskManager;
            _repo = repo;
            _logger = logger;
            _libraryMonitor = libraryMonitor;
            _libraryManager = libraryManager;
            _config = config;
            _fileSystem = fileSystem;
            _providerManager = providerManager;
            _serverManager = serverManager;
            _localizationManager = localizationManager;
        }

        /// <summary>
        /// A collection of item ids which are currently being processed.
        /// </summary>
        /// <remarks>Dictionary values are unused.</remarks>
        public ConcurrentDictionary<string, bool> InProgressItemIds
        {
            get
            {
                return _inProgressItemIds;
            }
        }

        public void BeginProcessNewFiles()
        {
            _taskManager.CancelIfRunningAndQueue<OrganizerScheduledTask>();
        }

        public Task SaveResult(FileOrganizationResult result, CancellationToken cancellationToken)
        {
            if (result == null || string.IsNullOrEmpty(result.OriginalPath))
            {
                throw new ArgumentNullException("result");
            }

            result.Id = result.OriginalPath.GetMD5().ToString("N");

            return _repo.SaveResult(result, cancellationToken);
        }

        public QueryResult<FileOrganizationResult> GetResults(FileOrganizationResultQuery query)
        {
            var results = _repo.GetResults(query);

            foreach (var result in results.Items)
            {
                UpdateProgress(result);
            }

            return results;
        }

        public FileOrganizationResult GetResult(string id)
        {
            var result = _repo.GetResult(id);
            UpdateProgress(result);
            return result;
        }

        public FileOrganizationResult GetResultBySourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            var id = GetResultIdFromSourcePath(path);

            return GetResult(id);
        }

        public string GetResultIdFromSourcePath(string path)
        {
            return path.GetMD5().ToString("N");
        }


        public Task DeleteOriginalFile(string resultId)
        {
            var result = _repo.GetResult(resultId);

            using (new ItemProgressLock(result.Id, _inProgressItemIds, _serverManager, _localizationManager))
            {
                _logger.Info("Requested to delete {0}", result.OriginalPath);
                try
                {
                    var file = _fileSystem.GetFileInfo(result.OriginalPath);
                    _fileSystem.DeleteFile(result.OriginalPath);

                    var organizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                    _libraryMonitor, _providerManager, _serverManager, _localizationManager);

                    if (file != null && file.Exists && file.DirectoryName != null)
                    {
                        organizer.DeleteLeftoverFilesAndEmptyFolders(GetAutoOrganizeOptions(), file.DirectoryName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error deleting {0}", ex, result.OriginalPath);
                }

                var task = _repo.Delete(resultId);

                return task;
            }
        }

        private AutoOrganizeOptions GetAutoOrganizeOptions()
        {
            return _config.GetAutoOrganizeOptions();
        }

        public async Task PerformOrganization(string resultId)
        {
            // This function is not purely async. To workaround this, use .Yield() to immediately return to the caller
            await Task.Yield();

            var result = _repo.GetResult(resultId);

            if (string.IsNullOrEmpty(result.TargetPath))
            {
                throw new ArgumentException("No target path available.");
            }

            var options = GetAutoOrganizeOptions();

            switch (result.Type)
            {
                case FileOrganizerType.Episode:

                    var organizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                        _libraryMonitor, _providerManager, _serverManager, _localizationManager);

                    var file = _fileSystem.GetFileInfo(result.OriginalPath);

                    var task = await organizer.OrganizeFile(result.OriginalPath, options, true, CancellationToken.None)
                            .ConfigureAwait(false);


                    if (file != null && file.Exists && file.DirectoryName != null)
                    {
                        organizer.DeleteLeftoverFilesAndEmptyFolders(options, file.DirectoryName);
                    }

                    break;

                case FileOrganizerType.Movie:
                    var movieOrganizer = new MovieFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                        _libraryMonitor, _providerManager, _serverManager, _localizationManager);

                    var movieFile = _fileSystem.GetFileInfo(result.OriginalPath);

                    var task2 = await movieOrganizer.OrganizeFile(result.OriginalPath, GetAutoOrganizeOptions(), true, CancellationToken.None)
                            .ConfigureAwait(false);


                    if (movieFile != null && movieFile.Exists && movieFile.DirectoryName != null)
                    {
                        movieOrganizer.DeleteLeftoverFilesAndEmptyFolders(options, movieFile.DirectoryName);
                    }

                    break;

                default:
                    throw new ArgumentException("Unsupported organizer type.");
            }
        }

        public Task ClearLog()
        {
            var task = _repo.DeleteAll();
            _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => task, CancellationToken.None);
            return task;
        }

        public async Task PerformEpisodeOrganization(EpisodeFileOrganizationRequest request)
        {
            // The OrganizeWithCorrection function is not purely async. To workaround this, use .Yield() to immediately return to the caller
            await Task.Yield();

            var options = GetAutoOrganizeOptions();

            var organizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                _libraryMonitor, _providerManager, _serverManager, _localizationManager);

            await organizer.OrganizeWithCorrection(request, options, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task PerformMovieOrganization(MovieFileOrganizationRequest request)
        {
            // The OrganizeWithCorrection function is not purely async. To workaround this, use .Yield() to immediately return to the caller
            await Task.Yield();

            var organizer = new MovieFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                _libraryMonitor, _providerManager, _serverManager, _localizationManager);

            await organizer.OrganizeWithCorrection(request, GetAutoOrganizeOptions(), CancellationToken.None).ConfigureAwait(false); ;
        }

        public QueryResult<SmartMatchInfo> GetSmartMatchInfos(FileOrganizationResultQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var options = GetAutoOrganizeOptions();

            var items = options.SmartMatchOptions.SmartMatchInfos.Skip(query.StartIndex ?? 0).Take(query.Limit ?? Int32.MaxValue);

            return new QueryResult<SmartMatchInfo>()
            {
                Items = items.ToArray(),
                TotalRecordCount = items.Count()
            };
        }

        public void DeleteSmartMatchEntry(string IdString, string matchString)
        {
            Guid Id;

            if (!Guid.TryParse(IdString, out Id))
            {
                throw new ArgumentNullException("Id");
            }

            if (string.IsNullOrEmpty(matchString))
            {
                throw new ArgumentNullException("matchString");
            }

            var options = GetAutoOrganizeOptions();

            SmartMatchInfo info = options.SmartMatchOptions.SmartMatchInfos.Find(i => i.Id == Id);

            if (info != null && info.MatchStrings.Contains(matchString))
            {
                info.MatchStrings.Remove(matchString);
                if (info.MatchStrings.Count == 0)
                {
                    options.SmartMatchOptions.SmartMatchInfos.Remove(info);
                }

                _config.SaveAutoOrganizeOptions(options);
            }
        }

        private void UpdateProgress(FileOrganizationResult result)
        {
            if (result != null)
            {
                result.IsInProgress = _inProgressItemIds.ContainsKey(result.Id);
            }
        }
    }
}
