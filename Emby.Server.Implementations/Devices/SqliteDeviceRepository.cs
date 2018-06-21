using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Emby.Server.Implementations.Data;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using SQLitePCL.pretty;
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
    public class SqliteDeviceRepository : BaseSqliteRepository, IDeviceRepository
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        protected IFileSystem FileSystem { get; private set; }
        private readonly object _cameraUploadSyncLock = new object();
        private readonly object _capabilitiesSyncLock = new object();
        private readonly IJsonSerializer _json;
        private IServerApplicationPaths _appPaths;

        private bool _enableDatabase;

        public SqliteDeviceRepository(ILogger logger, IServerConfigurationManager config, IFileSystem fileSystem, IJsonSerializer json)
            : base(logger)
        {
            var appPaths = config.ApplicationPaths;

            DbFilePath = Path.Combine(appPaths.DataPath, "devices.db");
            FileSystem = fileSystem;
            _json = json;
            _appPaths = appPaths;

        }

        public void Initialize()
        {
            _enableDatabase = FileSystem.FileExists(DbFilePath);

            if (_enableDatabase)
            {
                try
                {
                    using (var connection = CreateConnection())
                    {
                        RunDefaultInitialization(connection);

                        string[] queries = {
                    "create table if not exists Devices (Id TEXT PRIMARY KEY, Name TEXT NOT NULL, ReportedName TEXT NOT NULL, CustomName TEXT, CameraUploadPath TEXT, LastUserName TEXT, AppName TEXT NOT NULL, AppVersion TEXT NOT NULL, LastUserId TEXT, DateLastModified DATETIME NOT NULL, Capabilities TEXT NOT NULL)",
                    "create index if not exists idx_id on Devices(Id)"
                               };

                        connection.RunQueries(queries);
                    }
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error loading database file. Will reset and retry.", ex);

                    FileSystem.DeleteFile(DbFilePath);

                    _enableDatabase = false;
                }
            }
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

            if (_enableDatabase)
            {
                using (WriteLock.Read())
                {
                    using (var connection = CreateConnection(true))
                    {
                        var statementTexts = new List<string>();
                        statementTexts.Add("Select Capabilities from Devices where Id=@Id");

                        return connection.RunInTransaction(db =>
                        {
                            var statements = PrepareAllSafe(db, statementTexts).ToList();

                            using (var statement = statements[0])
                            {
                                statement.TryBind("@Id", id);

                                foreach (var row in statement.ExecuteQuery())
                                {
                                    if (row[0].SQLiteType != SQLiteType.Null)
                                    {
                                        return _json.DeserializeFromString<ClientCapabilities>(row.GetString(0));
                                    }
                                }

                                return new ClientCapabilities();
                            }

                        }, ReadTransactionMode);
                    }
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
