using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.FileOrganization;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.FileOrganization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Server.Implementations.Library;
using MediaBrowser.Server.Implementations.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Localization;

namespace MediaBrowser.Server.Implementations.FileOrganization
{
    public abstract class FileOrganizerBase
    {
        protected readonly ILibraryMonitor _libraryMonitor;
        protected readonly ILibraryManager _libraryManager;
        protected readonly ILogger _logger;
        protected readonly IFileSystem _fileSystem;
        protected readonly IFileOrganizationService _organizationService;
        protected readonly IServerConfigurationManager _config;
        protected readonly IProviderManager _providerManager;
        protected readonly IServerManager _serverManager;
        protected readonly ILocalizationManager _localizationManager;

        protected readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public FileOrganizerBase(IFileOrganizationService organizationService, IServerConfigurationManager config, IFileSystem fileSystem, ILogger logger, ILibraryManager libraryManager, ILibraryMonitor libraryMonitor, IProviderManager providerManager, IServerManager serverManager, ILocalizationManager localizationManager)
        {
            _organizationService = organizationService;
            _config = config;
            _fileSystem = fileSystem;
            _logger = logger;
            _libraryManager = libraryManager;
            _libraryMonitor = libraryMonitor;
            _providerManager = providerManager;
            _serverManager = serverManager;
            _localizationManager = localizationManager;
        }

        public static string GetTitleAsSearchTerm(string title)
        {
            const string ReplaceChars = "!\"§$%&/\\(){}[]=?´`°^'#+-/*@,.-;:_<>|";
            if (title != null)
            {
                foreach (Char c in ReplaceChars)
                {
                    title = title.Replace(c, ' ');
                }
            }

            title = title.Replace("  ", " ");
            title = title.Replace("  ", " ");

            return title;
        }

        public abstract Task<FileOrganizationResult> OrganizeFile(string path, AutoOrganizeOptions options, bool overwriteExisting, CancellationToken cancellationToken);

        public abstract Task<FileOrganizationResult> OrganizeWithCorrection(BaseFileOrganizationRequest request, AutoOrganizeOptions options, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes leftover files and empty folders if configured to do so.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="watchLocations"></param>
        /// <param name="folderPath"></param>
        public void DeleteLeftoverFilesAndEmptyFolders(AutoOrganizeOptions options, string folderPath)
        {
            var deleteExtensions = options.TvOptions.LeftOverFileExtensionsToDelete
                .Select(i => i.Trim().TrimStart('.'))
                .Where(i => !string.IsNullOrEmpty(i))
                .Select(i => "." + i)
                .ToList();

            if (deleteExtensions.Count > 0)
            {
                DeleteLeftOverFiles(folderPath, deleteExtensions);
            }

            if (options.TvOptions.DeleteEmptyFolders)
            {
                if (!IsWatchFolder(folderPath, options.TvOptions.WatchLocations))
                {
                    DeleteEmptyFolders(folderPath);
                }
            }
        }

        /// <summary>
        /// Deletes the left over files.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="extensions">The extensions.</param>
        private void DeleteLeftOverFiles(string path, IEnumerable<string> extensions)
        {
            var eligibleFiles = _fileSystem.GetFiles(path, true)
                .Where(i => extensions.Contains(i.Extension, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in eligibleFiles)
            {
                try
                {
                    _fileSystem.DeleteFile(file.FullName);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error deleting file {0}", ex, file.FullName);
                }
            }
        }

        /// <summary>
        /// Deletes the empty folders.
        /// </summary>
        /// <param name="path">The path.</param>
        private void DeleteEmptyFolders(string path)
        {
            try
            {
                foreach (var d in _fileSystem.GetDirectoryPaths(path))
                {
                    DeleteEmptyFolders(d);
                }

                var entries = _fileSystem.GetFileSystemEntryPaths(path);

                if (!entries.Any())
                {
                    try
                    {
                        _logger.Debug("Deleting empty directory {0}", path);
                        _fileSystem.DeleteDirectory(path, false);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Determines if a given folder path is contained in a folder list
        /// </summary>
        /// <param name="path">The folder path to check.</param>
        /// <param name="watchLocations">A list of folders.</param>
        private bool IsWatchFolder(string path, IEnumerable<string> watchLocations)
        {
            return watchLocations.Contains(path, StringComparer.OrdinalIgnoreCase);
        }
    }
}
