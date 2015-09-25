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

        public FileOrganizationService(ITaskManager taskManager, IFileOrganizationRepository repo, ILogger logger, ILibraryMonitor libraryMonitor, ILibraryManager libraryManager, IServerConfigurationManager config, IFileSystem fileSystem, IProviderManager providerManager, IServerManager serverManager)
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
            return _repo.GetResults(query);
        }

        public FileOrganizationResult GetResult(string id)
        {
            return _repo.GetResult(id);
        }

        public FileOrganizationResult GetResultBySourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }
            
            var id = path.GetMD5().ToString("N");

            return GetResult(id);
        }

        public Task DeleteOriginalFile(string resultId)
        {
            var result = _repo.GetResult(resultId);

            _logger.Info("Requested to delete {0}", result.OriginalPath);
            try
            {
                _fileSystem.DeleteFile(result.OriginalPath);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error deleting {0}", ex, result.OriginalPath);
            }

            return _repo.Delete(resultId);
        }

        private AutoOrganizeOptions GetAutoOrganizeOptions()
        {
            return _config.GetAutoOrganizeOptions();
        }

        public async Task PerformOrganization(string resultId)
        {
            var result = _repo.GetResult(resultId);

            if (string.IsNullOrEmpty(result.TargetPath))
            {
                throw new ArgumentException("No target path available.");
            }

            switch (result.Type)
            {
                case FileOrganizerType.Episode:

                    var organizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                        _libraryMonitor, _providerManager);

                    var task = await organizer.OrganizeFile(result.OriginalPath, GetAutoOrganizeOptions(), true, CancellationToken.None)
                            .ConfigureAwait(false);
                    await _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => task, CancellationToken.None);
                    break;

                case FileOrganizerType.Movie:
                    var movieOrganizer = new MovieFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                        _libraryMonitor, _providerManager);

                    var task2 = await movieOrganizer.OrganizeFile(result.OriginalPath, GetAutoOrganizeOptions(), true, CancellationToken.None)
                            .ConfigureAwait(false);
                    await _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => task2, CancellationToken.None);
                    break;

                default:
                    throw new ArgumentException("Unsupported organizer type.");
            }
        }

        public Task ClearLog()
        {
            return _repo.DeleteAll();
        }

        public async Task PerformEpisodeOrganization(EpisodeFileOrganizationRequest request)
        {
            var organizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                _libraryMonitor, _providerManager);

            var task = await organizer.OrganizeWithCorrection(request, GetAutoOrganizeOptions(), CancellationToken.None).ConfigureAwait(false);

            await _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => task, CancellationToken.None);
        }

        public async Task PerformMovieOrganization(MovieFileOrganizationRequest request)
        {
            var organizer = new MovieFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                _libraryMonitor, _providerManager);

            var task = await organizer.OrganizeWithCorrection(request, GetAutoOrganizeOptions(), CancellationToken.None).ConfigureAwait(false);

            await _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => task, CancellationToken.None);
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
    }
}
