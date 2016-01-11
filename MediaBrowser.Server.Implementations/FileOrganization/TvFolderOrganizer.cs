using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.FileOrganization;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.FileOrganization;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Localization;

namespace MediaBrowser.Server.Implementations.FileOrganization
{
    public class TvFolderOrganizer
    {
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IFileOrganizationService _organizationService;
        private readonly IServerConfigurationManager _config;
        private readonly IProviderManager _providerManager;
        private readonly IServerManager _serverManager;
        private readonly ILocalizationManager _localizationManager;

        public TvFolderOrganizer(ILibraryManager libraryManager, ILogger logger, IFileSystem fileSystem, ILibraryMonitor libraryMonitor, IFileOrganizationService organizationService, IServerConfigurationManager config, IProviderManager providerManager, IServerManager serverManager, ILocalizationManager localizationManager)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
            _libraryMonitor = libraryMonitor;
            _organizationService = organizationService;
            _config = config;
            _providerManager = providerManager;
            _serverManager = serverManager;
            _localizationManager = localizationManager;
        }

        private bool EnableOrganization(FileSystemMetadata fileInfo, TvFileOrganizationOptions options)
        {
            var minFileBytes = options.MinFileSizeMb * 1024 * 1024;

            try
            {
                return _libraryManager.IsVideoFile(fileInfo.FullName) && fileInfo.Length >= minFileBytes;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error organizing file {0}", ex, fileInfo.Name);
            }

            return false;
        }

        public async Task Organize(AutoOrganizeOptions options, CancellationToken cancellationToken, IProgress<double> progress)
        {
            var watchLocations = options.TvOptions.WatchLocations.ToList();

            var eligibleFiles = watchLocations.SelectMany(GetFilesToOrganize)
                .OrderBy(_fileSystem.GetCreationTimeUtc)
                .Where(i => EnableOrganization(i, options.TvOptions))
                .ToList();

            var processedFolders = new HashSet<string>();

            // Even if this may seem like a waste of time - these 3s allow dynamic UI changes 
            // in the client UI. Without this delay, there won't be any visual feedback in the client.
            // This in turn can lead to the impression that there is a bug and the task hasn't even been executed
            await Task.Delay(1500).ConfigureAwait(false);
            progress.Report(10);
            await Task.Delay(1500).ConfigureAwait(false);


            if (eligibleFiles.Count > 0)
            {
                var numComplete = 0;

                foreach (var file in eligibleFiles)
                {
                    var organizer = new EpisodeFileOrganizer(_organizationService, _config, _fileSystem, _logger, _libraryManager,
                        _libraryMonitor, _providerManager, _serverManager, _localizationManager);

                    try
                    {
                        var result = await organizer.OrganizeFile(file.FullName, options, options.TvOptions.OverwriteExistingEpisodes, cancellationToken).ConfigureAwait(false);
                        if (result.Status == FileSortingStatus.Success && !processedFolders.Contains(file.DirectoryName, StringComparer.OrdinalIgnoreCase))
                        {
                            processedFolders.Add(file.DirectoryName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException("Error organizing episode {0}", ex, file);
                    }

                    numComplete++;
                    double percent = numComplete;
                    percent /= eligibleFiles.Count;

                    progress.Report(10 + (89 * percent));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(99);

            foreach (var folderPath in processedFolders)
            {
                var organizer = new EpisodeFileOrganizer(_organizationService, _config, _fileSystem, _logger, _libraryManager,
                    _libraryMonitor, _providerManager, _serverManager, _localizationManager);
                organizer.DeleteLeftoverFilesAndEmptyFolders(options, folderPath);
            }

            progress.Report(100);
        }

        /// <summary>
        /// Gets the files to organize.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>IEnumerable{FileInfo}.</returns>
        private List<FileSystemMetadata> GetFilesToOrganize(string path)
        {
            try
            {
                return _fileSystem.GetFiles(path, true)
                    .ToList();
            }
            catch (IOException ex)
            {
                _logger.ErrorException("Error getting files from {0}", ex, path);

                return new List<FileSystemMetadata>();
            }
        }
    }
}
