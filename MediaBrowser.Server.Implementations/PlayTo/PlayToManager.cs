using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Server.Implementations.PlayTo.Configuration;
using MediaBrowser.Server.Implementations.PlayTo.Helpers;
using MediaBrowser.Server.Implementations.PlayTo.Managed;
using MediaBrowser.Server.Implementations.PlayTo.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;

namespace MediaBrowser.Server.Implementations.PlayTo
{
    class PlayToManager : IDisposable
    {
        #region Fields

        private bool _disposed = false;
        private ILogger _logger;
        private ISessionManager _sessionManager;
        private User _defualtUser;
        private CancellationTokenSource _tokenSource;
        private ConcurrentDictionary<string, DateTime> _locations;
        private List<Task> _activeTasks;

        #endregion

        public PlayToManager(ILogger logger, ISessionManager sessionManager)
        {
            _activeTasks = new List<Task>();
            _logger = logger;
            _sessionManager = sessionManager;
            _locations = new ConcurrentDictionary<string, DateTime>();
            _tokenSource = new CancellationTokenSource();

        }

        public async void Start(User defaultUser)
        {            
            _defualtUser = defaultUser;
            _logger.Log(LogSeverity.Info, "PlayTo-Manager starting");

            _locations = new ConcurrentDictionary<string, DateTime>();

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            if (nics == null || nics.Length < 1)
            {
                _logger.Error("No network interfaces found.");
                return;
            }

            foreach (NetworkInterface network in nics)
            {
                _logger.Debug("Found interface: {0}. Type: {1}. Status: {2}", network.Name, network.NetworkInterfaceType, network.OperationalStatus);

                if (!network.SupportsMulticast ||
                    OperationalStatus.Up != network.OperationalStatus ||
                    !network.GetIPProperties().MulticastAddresses.Any())
                    continue;
                IPv4InterfaceProperties ipV4 = network.GetIPProperties().GetIPv4Properties();
                if (null == ipV4)
                    continue;

                IPAddress localIp = null;

                foreach (UnicastIPAddressInformation ipInfo in network.GetIPProperties().UnicastAddresses)
                {
                    if (ipInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        localIp = ipInfo.Address;
                        break;
                    }
                }

                if (localIp == null)
                {
                    continue;
                }

                try
                {
                    CreateListener(localIp);
                    //CreateNotifyer(localIp);
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Failed to Initilize Socket", e);
                }

                await Task.Delay(100);
            }
        }

        public void Stop()
        {
        }


        #region Socket & DLNA

        /// <summary>
        /// Creates a socket for the interface and listends for data.
        /// </summary>
        /// <param name="localIp">The local ip.</param>
        private void CreateListener(IPAddress localIp)
        {
            Task.Factory.StartNew(async (o) =>
            {
                try
                {
                    var socket = GetMulticastSocket();

                    socket.Bind(new System.Net.IPEndPoint(localIp, 0));
                    //var request = SsdpHelper.CreateRendererSSDP(3);
                    //socket.SendTo(request, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));
                    _logger.Info("Creating SSDP listener");

                    DateTime startTime = DateTime.UtcNow;

                    byte[] receiveBuffer = new byte[64000];

                    CreateNotifyer(socket);

                    while (!_tokenSource.IsCancellationRequested)
                    {
                        var receivedBytes = await socket.ReceiveAsync(receiveBuffer, 0, 64000);

                        if (receivedBytes > 0)
                        {
                            string rawData = Encoding.UTF8.GetString(receiveBuffer, 0, receivedBytes);
                            Uri uri = SsdpHelper.ParseSsdpResponse(rawData);

                            await CreateController(uri);

                        }
                    }
                    _logger.Info("SSDP listener - Task completed");
                }
                catch (TaskCanceledException c)
                {
                    _logger.Log(LogSeverity.Error, "DLNA PLugin - " + c.ToString());

                }
                catch (Exception e)
                {
                    _logger.Log(LogSeverity.Error, "DLNA PLugin - " + e.ToString());
                }

            }, _tokenSource.Token, TaskCreationOptions.LongRunning);

        }

        private void CreateNotifyer(Socket socket)
        {

            Task.Factory.StartNew(async (o) =>
            {
                try
                {
                    //var socket = GetNotifySocket();

                    //socket.Bind(new System.Net.IPEndPoint(localIp, 1900));
                    var request = SsdpHelper.CreateRendererSSDP(3);

                    while (true)
                    {
                        socket.SendTo(request, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));
                        await Task.Delay(10000);
                    }
                }
                catch (TaskCanceledException c)
                {
                    _logger.Log(LogSeverity.Error, "DLNA Plugin - " + c.ToString());

                }
                catch (Exception e)
                {
                    _logger.Log(LogSeverity.Error, "DLNA Plugin - " + e.ToString());
                }

            }, _tokenSource.Token, TaskCreationOptions.LongRunning);

        }

        /// <summary>
        /// Gets a socket configured for SDDP multicasting.
        /// </summary>
        /// <returns></returns>
        private Socket GetMulticastSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse("239.255.255.250")));
            //socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 3);
            return socket;
        }

        private Socket GetNotifySocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 3);
            return socket;
        }

        /// <summary>
        /// Creates a new DlnaSessionController.
        /// and logs the session in SessionManager
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns></returns>
        private async Task CreateController(Uri uri)
        {                       
            var device = await Device.CreateuPnpDeviceAsync(uri);

            if (device != null && device.RendererCommands != null && _sessionManager.Sessions != null && _sessionManager.Sessions.Where(s => s.RemoteEndPoint == uri.OriginalString && s.IsActive).Any() == false)
            {
                var transcodeProfiles = TranscodeSettings.GetProfileSettings(device.Properties);              
                var sessionInfo = await _sessionManager.LogSessionActivity(device.Properties.ClientType, device.Properties.Name, device.Properties.UUID, device.Properties.DisplayName, uri.OriginalString, _defualtUser);
                if (sessionInfo != null)
                {
                    ((PlayToController)sessionInfo.SessionController).Init(device, transcodeProfiles);
                    _logger.Log(LogSeverity.Info, "DLNA Session created for {0} - {1}", device.Properties.Name, device.Properties.ModelName);
                }
                else
                {
                    device.Dispose();
                }
            }
        }

        /// <summary>
        /// Determines if the Uri is valid for further inspection or not.
        /// (the limit for reinspection is 5 minutes)
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>Returns <b>True</b> if the Uri is valid for further inspection</returns>
        private bool IsUriValid(Uri uri)
        {
            if (uri == null)
                return false;

            if (!_locations.ContainsKey(uri.OriginalString))
            {
                _locations.AddOrUpdate(uri.OriginalString, DateTime.UtcNow, (key, existingVal) =>
                {
                    return existingVal;
                });

                return true;
            }

            var time = _locations[uri.OriginalString];
            if ((DateTime.UtcNow - time).TotalMinutes <= 5)
            {
                return false;
            }
            return _locations.TryUpdate(uri.OriginalString, DateTime.UtcNow, time);
        }

        #endregion


        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _tokenSource.Cancel();
                Task.WaitAll(_activeTasks.ToArray());
            }
        }
    }
}
