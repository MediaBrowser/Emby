﻿using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Connect;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using MediaBrowser.Common.IO;

namespace MediaBrowser.Server.Implementations.Connect
{
    public class ConnectEntryPoint : IServerEntryPoint
    {
        private Timer _timer;
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger _logger;
        private readonly IConnectManager _connectManager;

        private readonly INetworkManager _networkManager;
        private readonly IApplicationHost _appHost;
        private readonly IFileSystem _fileSystem;

        public ConnectEntryPoint(IHttpClient httpClient, IApplicationPaths appPaths, ILogger logger, INetworkManager networkManager, IConnectManager connectManager, IApplicationHost appHost, IFileSystem fileSystem)
        {
            _httpClient = httpClient;
            _appPaths = appPaths;
            _logger = logger;
            _networkManager = networkManager;
            _connectManager = connectManager;
            _appHost = appHost;
            _fileSystem = fileSystem;
        }

        public void Run()
        {
            LoadCachedAddress();

            _timer = new Timer(TimerCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromHours(3));
        }

        private readonly string[] _ipLookups = { "http://bot.whatismyipaddress.com", "https://connect.emby.media/service/ip" };

        private async void TimerCallback(object state)
        {
            foreach (var ipLookupUrl in _ipLookups)
            {
                try
                {
                    using (var stream = await _httpClient.Get(new HttpRequestOptions
                    {
                        Url = ipLookupUrl,
                        UserAgent = "Emby Server/" + _appHost.ApplicationVersion

                    }).ConfigureAwait(false))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var address = await reader.ReadToEndAsync().ConfigureAwait(false);

                            if (IsValid(address))
                            {
                                ((ConnectManager)_connectManager).OnWanAddressResolved(address);
                                CacheAddress(address);
                                return;
                            }
                        }
                    }
                }
                catch (HttpException)
                {
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error getting connection info", ex);
                }
            }
        }

        private string CacheFilePath
        {
            get { return Path.Combine(_appPaths.DataPath, "wan.txt"); }
        }

        private void CacheAddress(string address)
        {
            var path = CacheFilePath;

            try
            {
				_fileSystem.CreateDirectory(Path.GetDirectoryName(path));
				_fileSystem.WriteAllText(path, address, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error saving data", ex);
            }
        }

        private void LoadCachedAddress()
        {
            var path = CacheFilePath;

            try
            {
				var endpoint = _fileSystem.ReadAllText(path, Encoding.UTF8);

                if (IsValid(endpoint))
                {
                    ((ConnectManager)_connectManager).OnWanAddressResolved(endpoint);
                }
            }
            catch (IOException)
            {
                // File isn't there. no biggie
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error loading data", ex);
            }
        }

        private bool IsValid(string address)
        {
            IPAddress ipAddress;
            var valid = IPAddress.TryParse(address, out ipAddress);

            if (!valid)
            {
                _logger.Error("{0} is not a valid ip address", address);
            }

            return valid;
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
