using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Events;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Devices;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Controller.Security;

namespace Emby.Server.Implementations.Devices
{
    public class DeviceManager : IDeviceManager
    {
        private readonly IDeviceRepository _repo;
        private readonly IUserManager _userManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly IServerConfigurationManager _config;
        private readonly ILogger _logger;
        private readonly INetworkManager _network;
        private readonly ILibraryManager _libraryManager;
        private readonly ILocalizationManager _localizationManager;

        private readonly IAuthenticationRepository _authRepo;

        public event EventHandler<GenericEventArgs<CameraImageUploadInfo>> CameraImageUploaded;

        public DeviceManager(IAuthenticationRepository authRepo, IDeviceRepository repo, ILibraryManager libraryManager, ILocalizationManager localizationManager, IUserManager userManager, IFileSystem fileSystem, ILibraryMonitor libraryMonitor, IServerConfigurationManager config, ILogger logger, INetworkManager network)
        {
            _repo = repo;
            _userManager = userManager;
            _fileSystem = fileSystem;
            _libraryMonitor = libraryMonitor;
            _config = config;
            _logger = logger;
            _network = network;
            _libraryManager = libraryManager;
            _localizationManager = localizationManager;
            _authRepo = authRepo;
        }

        public void SaveCapabilities(string reportedId, ClientCapabilities capabilities)
        {
            _repo.SaveCapabilities(reportedId, capabilities);
        }

        public ClientCapabilities GetCapabilities(string reportedId)
        {
            return _repo.GetCapabilities(reportedId) ?? new ClientCapabilities();
        }

        public DeviceInfo GetDevice(string id)
        {
            return GetDevice(id, true);
        }

        private DeviceInfo GetDevice(string id, bool includeCapabilities)
        {
            var session = _authRepo.Get(new AuthenticationInfoQuery
            {
                DeviceId = id

            }).Items.FirstOrDefault();

            var device = session == null ? null : ToDeviceInfo(session);

            return device;
        }

        public QueryResult<DeviceInfo> GetDevices(DeviceQuery query)
        {
            var sessions = _authRepo.Get(new AuthenticationInfoQuery
            {
                //UserId = query.UserId
                IsActive = true,
                HasUser = true

            }).Items;

            if (query.SupportsSync.HasValue)
            {
                var val = query.SupportsSync.Value;

                sessions = sessions.Where(i => GetCapabilities(i.DeviceId).SupportsSync == val).ToArray();
            }

            if (!query.UserId.Equals(Guid.Empty))
            {
                var user = _userManager.GetUserById(query.UserId);

                sessions = sessions.Where(i => CanAccessDevice(user, i.DeviceId)).ToArray();
            }

            var array = sessions.Select(ToDeviceInfo).ToArray();

            return new QueryResult<DeviceInfo>
            {
                Items = array,
                TotalRecordCount = array.Length
            };
        }

        private DeviceInfo ToDeviceInfo(AuthenticationInfo authInfo)
        {
            return new DeviceInfo
            {
                AppName = authInfo.AppName,
                AppVersion = authInfo.AppVersion,
                Id = authInfo.DeviceId,
                LastUserId = authInfo.UserId,
                LastUserName = authInfo.UserName,
                Name = authInfo.DeviceName,
                DateLastActivity = authInfo.DateLastActivity
            };
        }

        public ContentUploadHistory GetCameraUploadHistory(string deviceId)
        {
            return _repo.GetCameraUploadHistory(deviceId);
        }

        public async Task AcceptCameraUpload(string deviceId, Stream stream, LocalFileInfo file)
        {
            var device = GetDevice(deviceId, false);
            var uploadPathInfo = GetUploadPath(device);

            var path = uploadPathInfo.Item1;

            if (!string.IsNullOrWhiteSpace(file.Album))
            {
                path = Path.Combine(path, _fileSystem.GetValidFilename(file.Album));
            }

            path = Path.Combine(path, file.Name);
            path = Path.ChangeExtension(path, MimeTypes.ToExtension(file.MimeType) ?? "jpg");

            _fileSystem.CreateDirectory(_fileSystem.GetDirectoryName(path));

            await EnsureLibraryFolder(uploadPathInfo.Item2, uploadPathInfo.Item3).ConfigureAwait(false);

            _libraryMonitor.ReportFileSystemChangeBeginning(path);

            try
            {
                using (var fs = _fileSystem.GetFileStream(path, FileOpenMode.Create, FileAccessMode.Write, FileShareMode.Read))
                {
                    await stream.CopyToAsync(fs).ConfigureAwait(false);
                }

                _repo.AddCameraUpload(deviceId, file);
            }
            finally
            {
                _libraryMonitor.ReportFileSystemChangeComplete(path, true);
            }

            if (CameraImageUploaded != null)
            {
                EventHelper.FireEventIfNotNull(CameraImageUploaded, this, new GenericEventArgs<CameraImageUploadInfo>
                {
                    Argument = new CameraImageUploadInfo
                    {
                        Device = device,
                        FileInfo = file
                    }
                }, _logger);
            }
        }

        internal Task EnsureLibraryFolder(string path, string name)
        {
            var existingFolders = _libraryManager
                .RootFolder
                .Children
                .OfType<Folder>()
                .Where(i => _fileSystem.AreEqual(path, i.Path) || _fileSystem.ContainsSubPath(i.Path, path))
                .ToList();

            if (existingFolders.Count > 0)
            {
                return Task.CompletedTask;
            }

            _fileSystem.CreateDirectory(path);

            var libraryOptions = new LibraryOptions
            {
                PathInfos = new[] { new MediaPathInfo { Path = path } },
                EnablePhotos = true,
                EnableRealtimeMonitor = false,
                SaveLocalMetadata = true
            };

            if (string.IsNullOrWhiteSpace(name))
            {
                name = _localizationManager.GetLocalizedString("HeaderCameraUploads");
            }

            return _libraryManager.AddVirtualFolder(name, CollectionType.HomeVideos, libraryOptions, true);
        }

        private Tuple<string, string, string> GetUploadPath(DeviceInfo device)
        {
            var config = _config.GetUploadOptions();
            var path = config.CameraUploadPath;

            if (string.IsNullOrWhiteSpace(path))
            {
                path = DefaultCameraUploadsPath;
            }

            var topLibraryPath = path;

            if (config.EnableCameraUploadSubfolders)
            {
                path = Path.Combine(path, _fileSystem.GetValidFilename(device.Name));
            }

            return new Tuple<string, string, string>(path, topLibraryPath, null);
        }

        internal string GetUploadsPath()
        {
            var config = _config.GetUploadOptions();
            var path = config.CameraUploadPath;

            if (string.IsNullOrWhiteSpace(path))
            {
                path = DefaultCameraUploadsPath;
            }

            return path;
        }

        private string DefaultCameraUploadsPath
        {
            get { return Path.Combine(_config.CommonApplicationPaths.DataPath, "camerauploads"); }
        }

        public bool CanAccessDevice(User user, string deviceId)
        {
            if (user == null)
            {
                throw new ArgumentException("user not found");
            }
            if (string.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentNullException("deviceId");
            }

            if (!CanAccessDevice(user.Policy, deviceId))
            {
                var capabilities = GetCapabilities(deviceId);

                if (capabilities != null && capabilities.SupportsPersistentIdentifier)
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanAccessDevice(UserPolicy policy, string id)
        {
            if (policy.EnableAllDevices)
            {
                return true;
            }

            if (policy.IsAdministrator)
            {
                return true;
            }

            return policy.EnabledDevices.Contains(id, StringComparer.OrdinalIgnoreCase);
        }
    }

    public class DeviceManagerEntryPoint : IServerEntryPoint
    {
        private readonly DeviceManager _deviceManager;
        private readonly IServerConfigurationManager _config;
        private readonly IFileSystem _fileSystem;
        private ILogger _logger;

        public DeviceManagerEntryPoint(IDeviceManager deviceManager, IServerConfigurationManager config, IFileSystem fileSystem, ILogger logger)
        {
            _deviceManager = (DeviceManager)deviceManager;
            _config = config;
            _fileSystem = fileSystem;
            _logger = logger;
        }

        public async void Run()
        {
            if (!_config.Configuration.CameraUploadUpgraded && _config.Configuration.IsStartupWizardCompleted)
            {
                var path = _deviceManager.GetUploadsPath();

                if (_fileSystem.DirectoryExists(path))
                {
                    try
                    {
                        await _deviceManager.EnsureLibraryFolder(path, null).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException("Error creating camera uploads library", ex);
                    }

                    _config.Configuration.CameraUploadUpgraded = true;
                    _config.SaveConfiguration();
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DeviceManagerEntryPoint() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class DevicesConfigStore : IConfigurationFactory
    {
        public IEnumerable<ConfigurationStore> GetConfigurations()
        {
            return new ConfigurationStore[]
            {
                new ConfigurationStore
                {
                     Key = "devices",
                     ConfigurationType = typeof(DevicesOptions)
                }
            };
        }
    }

    public static class UploadConfigExtension
    {
        public static DevicesOptions GetUploadOptions(this IConfigurationManager config)
        {
            return config.GetConfiguration<DevicesOptions>("devices");
        }
    }
}