using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.IO;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Model.Devices;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Session;
using MediaBrowser.Controller.Configuration;

namespace Emby.Server.Implementations.Devices
{
    public class SqliteDeviceRepository : IDeviceRepository
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        protected IFileSystem FileSystem { get; private set; }
        private readonly object _cameraUploadSyncLock = new object();
        private readonly object _capabilitiesSyncLock = new object();
        private readonly IJsonSerializer _json;
        private IServerApplicationPaths _appPaths;
        private ILogger _logger;

        public SqliteDeviceRepository(ILogger logger, IServerConfigurationManager config, IFileSystem fileSystem, IJsonSerializer json)
        {
            var appPaths = config.ApplicationPaths;

            FileSystem = fileSystem;
            _json = json;
            _logger = logger;
            _appPaths = appPaths;
        }

        public void SaveCapabilities(string deviceId, ClientCapabilities capabilities)
        {
            var path = Path.Combine(GetDevicePath(deviceId), "capabilities.json");
            FileSystem.CreateDirectory(FileSystem.GetDirectoryName(path));

            lock (_capabilitiesSyncLock)
            {
                _capabilitiesCache[deviceId] = capabilities;

                _json.SerializeToFile(capabilities, path);
            }
        }

        private Dictionary<string, ClientCapabilities> _capabilitiesCache = new Dictionary<string, ClientCapabilities>(StringComparer.OrdinalIgnoreCase);
        public ClientCapabilities GetCapabilities(string id)
        {
            lock (_capabilitiesSyncLock)
            {
                ClientCapabilities result;
                if (_capabilitiesCache.TryGetValue(id, out result))
                {
                    return result;
                }

                var path = Path.Combine(GetDevicePath(id), "capabilities.json");
                try
                {
                    return _json.DeserializeFromFile<ClientCapabilities>(path);
                }
                catch
                {
                }
            }

            return new ClientCapabilities();
        }

        private string GetDevicesPath()
        {
            return Path.Combine(_appPaths.DataPath, "devices");
        }

        private string GetDevicePath(string id)
        {
            return Path.Combine(GetDevicesPath(), id.GetMD5().ToString("N"));
        }

        public ContentUploadHistory GetCameraUploadHistory(string deviceId)
        {
            var path = Path.Combine(GetDevicePath(deviceId), "camerauploads.json");

            lock (_cameraUploadSyncLock)
            {
                try
                {
                    return _json.DeserializeFromFile<ContentUploadHistory>(path);
                }
                catch (IOException)
                {
                    return new ContentUploadHistory
                    {
                        DeviceId = deviceId
                    };
                }
            }
        }

        public void AddCameraUpload(string deviceId, LocalFileInfo file)
        {
            var path = Path.Combine(GetDevicePath(deviceId), "camerauploads.json");
            FileSystem.CreateDirectory(FileSystem.GetDirectoryName(path));

            lock (_cameraUploadSyncLock)
            {
                ContentUploadHistory history;

                try
                {
                    history = _json.DeserializeFromFile<ContentUploadHistory>(path);
                }
                catch (IOException)
                {
                    history = new ContentUploadHistory
                    {
                        DeviceId = deviceId
                    };
                }

                history.DeviceId = deviceId;

                var list = history.FilesUploaded.ToList();
                list.Add(file);
                history.FilesUploaded = list.ToArray(list.Count);

                _json.SerializeToFile(history, path);
            }
        }
    }
}
