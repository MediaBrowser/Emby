using MediaBrowser.Common.Extensions;
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
using System.Collections.Concurrent; 
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
        private readonly ConcurrentDictionary<string, bool> _inProgressItemIds = new ConcurrentDictionary<string, bool>(); 

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
            var results = _repo.GetResults(query);

            foreach (var result in results.Items)
            {
                result.IsInProgress = _inProgressItemIds.ContainsKey(result.Id);
            }

            return results;
        }

        public FileOrganizationResult GetResult(string id)
        {
            var result = _repo.GetResult(id);

            if (result != null)
            {
                result.IsInProgress = _inProgressItemIds.ContainsKey(result.Id);
            }

            return result;
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

            if (!AddToInProgressList(result))
            {
                throw new Exception("Path is currently processed otherwise. Please try again later.");
            }

            try
            {
                _fileSystem.DeleteFile(result.OriginalPath);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error deleting {0}", ex, result.OriginalPath);
            }
            finally
            {
                RemoveFromInprogressList(result);
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

            var organizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                _libraryMonitor, _providerManager);

            await organizer.OrganizeEpisodeFile(result.OriginalPath, GetAutoOrganizeOptions(), true, CancellationToken.None)
                    .ConfigureAwait(false);
        }

        public Task ClearLog()
        {
            var task = _repo.DeleteAll();
            _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => task, CancellationToken.None); 
            return task;
        }

        public async Task PerformEpisodeOrganization(EpisodeFileOrganizationRequest request)
        {
            var organizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                _libraryMonitor, _providerManager);

            await organizer.OrganizeWithCorrection(request, GetAutoOrganizeOptions(), CancellationToken.None).ConfigureAwait(false);
        }

        public QueryResult<SmartMatchInfo> GetSmartMatchInfos(FileOrganizationResultQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var options = GetAutoOrganizeOptions();

            var items = options.SmartMatchInfos.Skip(query.StartIndex ?? 0).Take(query.Limit ?? Int32.MaxValue).ToArray();

            return new QueryResult<SmartMatchInfo>()
            {
                Items = items,
                TotalRecordCount = options.SmartMatchInfos.Length
            };
        }

        public void DeleteSmartMatchEntry(string itemName, string matchString)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                throw new ArgumentNullException("itemName");
            }

            if (string.IsNullOrEmpty(matchString))
            {
                throw new ArgumentNullException("matchString");
            }

            var options = GetAutoOrganizeOptions();

            SmartMatchInfo info = options.SmartMatchInfos.FirstOrDefault(i => string.Equals(i.ItemName, itemName));

            if (info != null && info.MatchStrings.Contains(matchString))
            {
                var list = info.MatchStrings.ToList();
                list.Remove(matchString);
                info.MatchStrings = list.ToArray();

                if (info.MatchStrings.Length == 0)
                {
                    var infos = options.SmartMatchInfos.ToList();
                    infos.Remove(info);
                    options.SmartMatchInfos = infos.ToArray();
                }

                _config.SaveAutoOrganizeOptions(options);
            }
        }

        /// <summary>
        /// Attempts to add a an item to the list of currently processed items.
        /// </summary>
        /// <param name="result">The result item.</param>
        /// <param name="notifyClient">Passing true will send a websocket message to the client.</param>
        /// <returns>True if the item was added, False if the item is already contained in the list.</returns>
        public bool AddToInProgressList(FileOrganizationResult result, bool notifyClient = false)
        {
            if (string.IsNullOrWhiteSpace(result.Id))
            {
                result.Id = result.OriginalPath.GetMD5().ToString("N");
            }

            if (!_inProgressItemIds.TryAdd(result.Id, false))
            {
                return false;
            }

            if (_serverManager != null && notifyClient)
            {
                _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => string.Empty, CancellationToken.None); 
            }

            return true;
        }

        /// <summary>
        /// Removes an item from the list of currently processed items.
        /// </summary>
        /// <param name="result">The result item.</param>
        /// <param name="notifyClient">Passing true will send a websocket message to the client.</param>
        /// <returns>True if the item was removed, False if the item was not contained in the list.</returns>
        public bool RemoveFromInprogressList(FileOrganizationResult result, bool notifyClient = false)
        {
            bool itemValue;
            var retval = _inProgressItemIds.TryRemove(result.Id, out itemValue);

            if (_serverManager != null && notifyClient)
            {
                _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => string.Empty, CancellationToken.None); 
            }

            return retval;
        }

    }
}
