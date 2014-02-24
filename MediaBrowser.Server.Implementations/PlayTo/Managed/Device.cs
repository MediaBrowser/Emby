using MediaBrowser.Server.Implementations.PlayTo.Managed.Entities;
using MediaBrowser.Server.Implementations.PlayTo.Managed.Events;
using MediaBrowser.Server.Implementations.PlayTo.Managed.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed
{
    public sealed class Device : IDisposable
    {
        const string SERVICE_AVTRANSPORT_ID = "urn:upnp-org:serviceId:AVTransport";
        const string SERVICE_RENDERING_ID = "urn:upnp-org:serviceId:RenderingControl";

        #region Fields & Properties

        Timer dt;

        private DeviceProperties _properties;
        public DeviceProperties Properties
        {
            get
            {
                return _properties;
            }
            set
            {
                _properties = value;
            }
        }

        private int _muteVol = 0;
        public bool IsMuted
        {
            get
            {
                return _muteVol > 0;
            }
        }

        string _currentId = string.Empty;
        public string CurrentId
        {
            get
            {
                return _currentId;
            }
            set
            {
                if (_currentId == value)
                    return;
                _currentId = value;
                NotifyCurrentIdChanged(value);
            }
        }

        private int _volume;
        public int Volume
        {
            get
            {
                return _volume;
            }
            set
            {
                _volume = value;
            }
        }

        private TimeSpan _duration;
        public TimeSpan Duration
        {
            get
            {
                return _duration;
            }
            set
            {
                _duration = value;
            }
        }

        private TimeSpan _position = TimeSpan.FromSeconds(0);
        public TimeSpan Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        private string _transportState = string.Empty;
        public string TransportState
        {
            get
            {
                return _transportState;
            }
            set
            {
                if (_transportState == value)
                    return;
                _transportState = value;

                if (value == "PLAYING" || value == "STOPPED")
                    NotifyPlaybackChanged(value == "STOPPED");
            }
        }

        public bool IsPlaying
        {
            get
            {
                return TransportState == "PLAYING";
            }
        }

        public bool IsTransitioning
        {
            get
            {
                return (TransportState == "TRANSITIONING") ? true : false;
            }
        }

        public bool IsPaused
        {
            get
            {
                if (TransportState == "PAUSED" || TransportState == "PAUSED_PLAYBACK")
                    return true;
                return false;
            }
        }

        public bool IsStopped
        {
            get
            {
                return (TransportState == "STOPPED") ? true : false;
            }
        }

        public DateTime UpdateTime
        { get; private set; }

        #endregion

        #region Constructor & Initializer

        public Device(DeviceProperties deviceProperties)
        {
            _properties = deviceProperties;
        }

        internal void Start()
        {
            UpdateTime = DateTime.UtcNow;
            dt = new Timer(1000);
            dt.Elapsed += dt_Elapsed;
            dt.Start();
        }

        #endregion

        #region Commanding

        public Task<bool> VolumeDown(bool mute = false)
        {
            var sendVolume = (Volume - 5) > 0 ? Volume - 5 : 0;
            if (mute && _muteVol == 0)
            {
                sendVolume = 0;
                _muteVol = Volume;
            }
            return SetVolume(sendVolume);
        }

        public Task<bool> VolumeUp(bool unmute = false)
        {
            var sendVolume = (Volume + 5) < 100 ? Volume + 5 : 100;
            if (unmute && _muteVol > 0)
                sendVolume = _muteVol;
            _muteVol = 0;
            return SetVolume(sendVolume);
        }

        public Task ToggleMute()
        {
            if (_muteVol == 0)
            {
                _muteVol = Volume;
                return SetVolume(0);
            }

            int tmp = _muteVol;
            _muteVol = 0;
            return SetVolume(tmp);

        }

        public async Task<bool> SetVolume(int value)
        {
            var command = RendererCommands.ServiceActions.Where(c => c.Name == "SetVolume").FirstOrDefault();
            if (command == null)
                return true;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_RENDERING_ID).FirstOrDefault();

            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType, value));
            Volume = value;
            return true;
        }

        public async Task<TimeSpan> Seek(TimeSpan value)
        {
            var command = AvCommands.ServiceActions.Where(c => c.Name == "Seek").FirstOrDefault();
            if (command == null)
                return value;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();

            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, AvCommands.BuildPost(command, service.ServiceType, string.Format("{0:hh}:{0:mm}:{0:ss}", value), "REL_TIME"));
            return value;
        }

        public async Task<bool> SetAvTransport(string url, string header, string metaData)
        {
            dt.Stop();
            TransportState = "STOPPED";
            CurrentId = "0";
            await Task.Delay(50);
            var command = AvCommands.ServiceActions.Where(c => c.Name == "SetAVTransportURI").FirstOrDefault();
            if (command == null)
                return false;

            var dictionary = new Dictionary<string, string>();
            dictionary.Add("CurrentURI", url);
            dictionary.Add("CurrentURIMetaData", SsdpHttpClient.CreateDidlMeta(metaData));

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();
            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, AvCommands.BuildPost(command, service.ServiceType, url, dictionary), header);
            if (!IsPlaying)
            {
                await Task.Delay(50);
                await SetPlay();
            }
            count = 5;
            dt.Start();
            return true;
        }

        public async Task<bool> SetNextAvTransport(string value, string header, string metaData)
        {
            var command = AvCommands.ServiceActions.Where(c => c.Name == "SetNextAVTransportURI").FirstOrDefault();
            if (command == null)
                return false;

            var dictionary = new Dictionary<string, string>();
            dictionary.Add("NextURI", value);
            dictionary.Add("NextURIMetaData", SsdpHttpClient.CreateDidlMeta(metaData));

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();
            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, AvCommands.BuildPost(command, service.ServiceType, value, dictionary), header);            
            await Task.Delay(100);
            return true;
        }

        public async Task<bool> SetPlay()
        {
            var command = AvCommands.ServiceActions.Where(c => c.Name == "Play").FirstOrDefault();
            if (command == null)
                return false;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();

            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType, 1));
            count = 5;
            return true;
        }

        public async Task<bool> SetStop()
        {
            var command = AvCommands.ServiceActions.Where(c => c.Name == "Stop").FirstOrDefault();
            if (command == null)
                return false;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();

            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType, 1));
            await Task.Delay(50);
            count = 4;
            return true;
        }

        public async Task<bool> SetPause()
        {
            var command = AvCommands.ServiceActions.Where(c => c.Name == "Pause").FirstOrDefault();
            if (command == null)
                return false;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();

            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType, 0));
            await Task.Delay(50);
            TransportState = "PAUSED_PLAYBACK";
            return true;
        }

        #endregion

        #region Get data

        int count = 5;
        async void dt_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            ((Timer)sender).Stop();
            var hasTrack = await GetPositionInfo();
            if (count > 4)
            {

                await GetTransportInfo();
                if (!hasTrack)
                {
                    await GetMediaInfo();
                }
                await GetVolume();
                count = 0;
            }

            count++;
            if (_disposed)
                return;
            ((Timer)sender).Start();
        }

        private async Task GetVolume()
        {
            var command = RendererCommands.ServiceActions.Where(c => c.Name == "GetVolume").FirstOrDefault();
            if (command == null)
                return;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_RENDERING_ID).FirstOrDefault();
            try
            {
                var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType));
                if (result == null)
                    return;
                var volume = result.Document.Descendants(uPnpNamespaces.RenderingControl + "GetVolumeResponse").FirstOrDefault().Element("CurrentVolume").Value;
                if (volume == null)
                    return;
                Volume = int.Parse(volume);

                //Reset the Mute value if Volume is bigger than zero
                if (Volume > 0 && _muteVol > 0)
                {
                    _muteVol = 0;
                }
            }
            catch { }
        }

        private async Task GetTransportInfo()
        {
            var command = AvCommands.ServiceActions.Where(c => c.Name == "GetTransportInfo").FirstOrDefault();
            if (command == null)
                return;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();
            if (service == null)
                return;

            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType));
            try
            {
                var transportState = result.Document.Descendants(uPnpNamespaces.AvTransport + "GetTransportInfoResponse").FirstOrDefault().Element("CurrentTransportState").Value;
                if (transportState != null)
                    TransportState = transportState;
            }
            catch { }

            if (result != null)
                UpdateTime = DateTime.UtcNow;
        }

        private async Task GetMediaInfo()
        {
            var command = AvCommands.ServiceActions.Where(c => c.Name == "GetMediaInfo").FirstOrDefault();
            if (command == null)
                return;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();

            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType));
            try
            {
                var track = result.Document.Descendants("CurrentURIMetaData").FirstOrDefault().Value;
                if (string.IsNullOrEmpty(track))
                {
                    CurrentId = "0";
                    return;
                }
                XElement uPnpResponse = XElement.Parse((String)track);

                var e = uPnpResponse.Element(uPnpNamespaces.items);

                if (e == null)
                    e = uPnpResponse;

                var uTrack = uParser.CreateObjectFromXML(new uParserObject { Type = e.Element(uPnpNamespaces.uClass).Value, Element = e });
                if (uTrack != null)
                    CurrentId = uTrack.Id;

            }
            catch { }            
        }

        private async Task<bool> GetPositionInfo()
        {
            var command = AvCommands.ServiceActions.Where(c => c.Name == "GetPositionInfo").FirstOrDefault();
            if (command == null)
                return true;

            var service = this.Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();

            var result = await SsdpHttpClient.SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType));
            try
            {
                var duration = result.Document.Descendants(uPnpNamespaces.AvTransport + "GetPositionInfoResponse").FirstOrDefault().Element("TrackDuration").Value;
                if (duration != null)
                {
                    Duration = TimeSpan.Parse(duration);
                }
                
                var position = result.Document.Descendants(uPnpNamespaces.AvTransport + "GetPositionInfoResponse").FirstOrDefault().Element("RelTime").Value;
                if (position != null)
                {
                    Position = TimeSpan.Parse(position);
                }
                
                var track = result.Document.Descendants("TrackMetaData").FirstOrDefault().Value;

                if (string.IsNullOrEmpty(track))
                {
                    //If track is null, some vendors do this, use GetMediaInfo instead                    
                    return false;
                }

                XElement uPnpResponse = XElement.Parse((String)track);

                var e = uPnpResponse.Element(uPnpNamespaces.items);

                if (e == null)
                    e = uPnpResponse;

                uBaseObject uTrack = uBaseObject.Create(e);
                if (uTrack == null)
                    return true;

                CurrentId = uTrack.Id;

                return true;
            }
            catch { return false; }                        
        }

        #endregion

        #region From XML

        internal async Task GetAVProtocolAsync()
        {

            var avService = Properties.Services.Where(s => s.ServiceId == SERVICE_AVTRANSPORT_ID).FirstOrDefault();
            if (avService == null)
                return;

            string url = avService.SCPDURL;
            if (!url.Contains("/"))
                url = "/dmr/" + url;
            if (!url.StartsWith("/"))
                url = "/" + url;

            var httpClient = new SsdpHttpClient();
            var stream = await httpClient.GetDataAsync(new Uri(Properties.BaseUrl + url));

            if (stream == null)
                return;

            XDocument document = httpClient.ParseStream(stream);
            stream.Dispose();

            AvCommands = TransportCommands.CreateFromXml(document);
        }

        internal async Task GetRenderingProtocolAsync()
        {
            var avService = Properties.Services.Where(s => s.ServiceId == SERVICE_RENDERING_ID).FirstOrDefault();

            if (avService == null)
                return;
            string url = avService.SCPDURL;
            if (!url.Contains("/"))
                url = "/dmr/" + url;
            if (!url.StartsWith("/"))
                url = "/" + url;

            var httpClient = new SsdpHttpClient();
            var stream = await httpClient.GetDataAsync(new Uri(Properties.BaseUrl + url));

            if (stream == null)
                return;

            XDocument document = httpClient.ParseStream(stream);
            stream.Dispose();

            RendererCommands = TransportCommands.CreateFromXml(document);
        }

        internal Transport.TransportCommands AvCommands
        {
            get;
            set;
        }

        internal Transport.TransportCommands RendererCommands
        {
            get;
            set;
        }

        public static async Task<Device> CreateuPnpDeviceAsync(Uri url)
        {

            var httpClient = new SsdpHttpClient();
            var stream = await httpClient.GetDataAsync(url);

            if (stream == null)
                return null;

            XDocument document = httpClient.ParseStream(stream);
            stream.Dispose();

            XNode node = document.Root;

            var deviceProperties = new DeviceProperties();

            var name = document.Descendants(uPnpNamespaces.ud.GetName("friendlyName")).FirstOrDefault();
            if (name != null)
                deviceProperties.Name = name.Value;

            var name2 = document.Descendants(uPnpNamespaces.ud.GetName("roomName")).FirstOrDefault();
            if (name2 != null)
                deviceProperties.Name = name2.Value;

            var model = document.Descendants(uPnpNamespaces.ud.GetName("modelName")).FirstOrDefault();
            if (model != null)
                deviceProperties.ModelName = model.Value;

            var modelNumber = document.Descendants(uPnpNamespaces.ud.GetName("modelNumber")).FirstOrDefault();
            if (modelNumber != null)
                deviceProperties.ModelNumber = modelNumber.Value;

            var uuid = document.Descendants(uPnpNamespaces.ud.GetName("UDN")).FirstOrDefault();
            if (uuid != null)
                deviceProperties.UUID = uuid.Value;

            var manufacturer = document.Descendants(uPnpNamespaces.ud.GetName("manufacturer")).FirstOrDefault();
            if (manufacturer != null)
                deviceProperties.Manufacturer = manufacturer.Value;

            var manufacturerUrl = document.Descendants(uPnpNamespaces.ud.GetName("manufacturerURL")).FirstOrDefault();
            if (manufacturerUrl != null)
                deviceProperties.ManufacturerUrl = manufacturerUrl.Value;

            var presentationUrl = document.Descendants(uPnpNamespaces.ud.GetName("presentationURL")).FirstOrDefault();
            if (presentationUrl != null)
                deviceProperties.PresentationUrl = presentationUrl.Value;


            deviceProperties.BaseUrl = string.Format("http://{0}:{1}", url.Host, url.Port);

            var icon = document.Descendants(uPnpNamespaces.ud.GetName("icon")).FirstOrDefault();

            if (icon != null)
            {
                deviceProperties.Icon = uIcon.FromXml(icon);
            }

            bool isRenderer = false;

            foreach (XElement services in document.Descendants(uPnpNamespaces.ud.GetName("serviceList")))
            {
                if (services == null)
                    return null;

                var servicesList = services.Descendants(uPnpNamespaces.ud.GetName("service"));
                if (servicesList == null)
                    return null;

                foreach (XElement element in servicesList)
                {
                    var service = uService.FromXml(element);
                    if (service != null)
                    {
                        deviceProperties.Services.Add(service);
                        if (service.ServiceId == SERVICE_AVTRANSPORT_ID)
                        {
                            isRenderer = true;
                        }
                    }
                }
            }

            if (isRenderer)
            {

                var device = new Device(deviceProperties);

                await device.GetRenderingProtocolAsync();
                await device.GetAVProtocolAsync();
                return device;
            }

            return null;
        }

        #endregion

        #region Events

        public event EventHandler<TransportStateEventArgs> PlaybackChanged;
        public event EventHandler<CurrentIdEventArgs> CurrentIdChanged;

        private void NotifyPlaybackChanged(bool value)
        {
            if (PlaybackChanged != null)
                PlaybackChanged.Invoke(this, new TransportStateEventArgs(this.IsStopped));
        }

        private void NotifyCurrentIdChanged(string value)
        {
            if (CurrentIdChanged != null)
                CurrentIdChanged.Invoke(this, new CurrentIdEventArgs(value));
        }

        #endregion

        #region IDisposable

        bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                dt.Stop();
            }
        }

        #endregion

        public override string ToString()
        {
            return string.Format("{0} - {1}", Properties.Name, Properties.BaseUrl);
        }
    }
}
