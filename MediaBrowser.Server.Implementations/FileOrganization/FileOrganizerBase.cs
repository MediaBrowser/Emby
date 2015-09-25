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

        protected readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public FileOrganizerBase(IFileOrganizationService organizationService, IServerConfigurationManager config, IFileSystem fileSystem, ILogger logger, ILibraryManager libraryManager, ILibraryMonitor libraryMonitor, IProviderManager providerManager)
        {
            _organizationService = organizationService;
            _config = config;
            _fileSystem = fileSystem;
            _logger = logger;
            _libraryManager = libraryManager;
            _libraryMonitor = libraryMonitor;
            _providerManager = providerManager;
        }

        public abstract Task<FileOrganizationResult> OrganizeFile(string path, AutoOrganizeOptions options, bool overwriteExisting, CancellationToken cancellationToken);

        public abstract Task<FileOrganizationResult> OrganizeWithCorrection(BaseFileOrganizationRequest request, AutoOrganizeOptions options, CancellationToken cancellationToken);
    }
}
