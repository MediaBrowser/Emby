using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.DlnaClientLibrary.Helpers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Server.Implementations.PlayTo;
using MediaBrowser.Server.Implementations.PlayTo.Configuration;
using MediaBrowser.Server.Implementations.PlayTo.Extensions;
using MediaBrowser.Server.Implementations.PlayTo.Managed;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.EntryPoints
{
    public class PlayToServerEntryPoint : IServerEntryPoint
    {
        #region Fields

        const string DEFAULT_USER = "PlayTo";

        private bool _disposed = false;
        private ILogger _logger;
        private IUserManager _userManager;
        private ISessionManager _sessionManager;
        private IXmlSerializer _xmlSerializer;
        private User _sessionUser;

        private CancellationTokenSource _tokenSource;

        private ConcurrentDictionary<string, DateTime> _locations;

        private List<Task> _activeTasks;

        #endregion

        public PlayToServerEntryPoint(ILogger logger, ISessionManager sessionManager, IUserManager userManager, IXmlSerializer xmlSerializer, INetworkManager networkManager)
        {
            _activeTasks = new List<Task>();
            _logger = logger;
            _userManager = userManager;
            _sessionManager = sessionManager;
            _xmlSerializer = xmlSerializer;
            _tokenSource = new CancellationTokenSource();
            _logger.Log(LogSeverity.Info, "PlayTo starting");

            CreateUserIfNeeded();


        }

        /// <summary>
        /// Creates the defaultuser if needed.
        /// </summary>
        private async void CreateUserIfNeeded()
        {
            var user = _userManager.Users.Where(u => u.Name == DEFAULT_USER).FirstOrDefault();
            if (user == null)
            {

                user = await _userManager.CreateUser(DEFAULT_USER);
                user.Configuration.IsHidden = true;
                user.Configuration.IsAdministrator = false;
                user.SaveConfiguration(_xmlSerializer);
                await _userManager.UpdateUser(user);

            }
            _sessionUser = user;
        }

        public void Run()
        {
            _locations = new ConcurrentDictionary<string, DateTime>();

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            if (nics == null || nics.Length < 1)
            {
                _logger.Log(LogSeverity.Error, "PlayTo - No network interfaces found.");
                return;
            }

            _logger.Log(LogSeverity.Info, "PlayTo started");

            foreach (NetworkInterface network in nics)
            {
                _logger.Log(LogSeverity.Debug, "PlayTo - Found interface: {0}. Type: {1}. Status: {2}", network.Name, network.NetworkInterfaceType, network.OperationalStatus);

                if (!network.SupportsMulticast)
                    continue;
                if (OperationalStatus.Up != network.OperationalStatus)
                    continue;
                if (!network.GetIPProperties().MulticastAddresses.Any())
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

                _logger.Log(LogSeverity.Info, "Creating socket for: " + localIp.ToString());
                try
                {
                    _activeTasks.Add(CreateListener(localIp));
                    _activeTasks.Add(CreateNotifyer(localIp));
                }
                catch (Exception e)
                {
                    _logger.Log(LogSeverity.Error, "Failed to bind socket: " + e.Message);
                }
            }
        }

        #region Socket & DLNA

        /// <summary>
        /// Creates a LongRunning task with a socket for the interface and listens for data.
        /// </summary>
        /// <param name="localIp">The local ip.</param>
        private Task CreateListener(IPAddress localIp)
        {
            return Task.Factory.StartNew(async (o) =>
             {
                 try
                 {
                     var socket = GetMulticastSocket();

                     socket.Bind(new System.Net.IPEndPoint(localIp, 0));
                     var request = SsdpHelper.CreateRendererSSDP(3);
                     socket.SendTo(request, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));
                     _logger.Log(LogSeverity.Info, "Creating SSDP listener");

                     DateTime startTime = DateTime.UtcNow;

                     byte[] receiveBuffer = new byte[64000];

                     while (true)
                     {
                         var receivedBytes = await socket.ReceiveAsync(receiveBuffer, 0, 64000);

                         if (receivedBytes > 0)
                         {
                             string rawData = Encoding.UTF8.GetString(receiveBuffer, 0, receivedBytes);
                             Uri uri = SsdpHelper.ParseSsdpResponse(rawData);
                             Console.WriteLine(rawData);
                             await CreateController(uri);
                         }
                     }
                     _logger.Log(LogSeverity.Info, "PlayTo - SSDP listener - Task completed");
                 }
                 catch (TaskCanceledException c)
                 {
                     _logger.Log(LogSeverity.Error, "PlayTo - SSDP listener - Task canceled");

                 }
                 catch (Exception e)
                 {
                     _logger.Log(LogSeverity.Error, "PlayTo PLugin - " + e.ToString());
                 }

             }, _tokenSource.Token, TaskCreationOptions.LongRunning);

        }

        private Task CreateNotifyer(IPAddress localIp)
        {
            return Task.Factory.StartNew(async (o) =>
            {
                try
                {
                    var socket = GetNotifySocket();

                    socket.Bind(new System.Net.IPEndPoint(localIp, 1900));
                    var request = SsdpHelper.CreateRendererSSDP(3);

                    while (true)
                    {
                        socket.SendTo(request, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));
                        await Task.Delay(5000);
                    }
                }
                catch (TaskCanceledException c)
                {
                    _logger.Log(LogSeverity.Error, "PlayTo - " + c.ToString());

                }
                catch (Exception e)
                {
                    _logger.Log(LogSeverity.Error, "PlayTo - " + e.ToString());
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
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 3);
            return socket;
        }

        /// <summary>
        /// Gets a socket configured for sending SDDP broadcast messages.
        /// </summary>
        /// <returns></returns>
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
        private Task CreateController(Uri uri)
        {
            if (IsUriValid(uri))
                return Task.FromResult(true);

            var device = Device.CreateuPnpDeviceAsync(uri);

            if (device.Result != null && device.Result.RendererCommands != null && _sessionManager.Sessions.Where(s => s.RemoteEndPoint == uri.OriginalString && s.IsActive).Any() == false)
            {
                var transcodeProfiles = TranscodeSettings.GetProfileSettings(device.Result.Properties);

                _logger.Debug("PlayTo - Detected device {0}. Trying to create Session", device.Result.Properties.DisplayName);
                var sessionInfo = _sessionManager.LogSessionActivity(device.Result.Properties.ClientType, device.Result.Properties.Name, device.Result.Properties.UUID, device.Result.Properties.DisplayName, uri.OriginalString, _sessionUser);

                ((PlayToController)sessionInfo.Result.SessionController).Init(device.Result, transcodeProfiles);
                _logger.Info("PlayTo - Session created for {0} - {1}", device.Result.Properties.Name, device.Result.Properties.ModelName);
            }
            return device;
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
            if ((DateTime.UtcNow - time).TotalMinutes <= 2)
            {
                return false;
            }
            return _locations.TryUpdate(uri.OriginalString, DateTime.UtcNow, time);
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _logger.Log(LogSeverity.Info, "Disposing PlayTo");
                _tokenSource.Cancel();
                Task.WaitAll(_activeTasks.ToArray(), 5000);
            }
        }

        #endregion
    }
}
