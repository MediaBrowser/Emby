using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Net;

namespace Emby.Server.Implementations.LiveTv.TunerHosts.HdHomerun
{
    public class LegacyHdHomerunHost : BaseTunerHost, ITunerHost, IConfigurableTunerHost
    {
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IServerApplicationHost _appHost;
        private readonly ISocketFactory _socketFactory;
        private readonly INetworkManager _networkManager;
        private Dictionary<string, string> _channelUrlMap = new Dictionary<string, string>();

        public LegacyHdHomerunHost(IServerConfigurationManager config, ILogger logger, IJsonSerializer jsonSerializer, IMediaEncoder mediaEncoder, IHttpClient httpClient, IFileSystem fileSystem, IServerApplicationHost appHost, ISocketFactory socketFactory, INetworkManager networkManager)
            : base(config, logger, jsonSerializer, mediaEncoder)
        {
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _appHost = appHost;
            _socketFactory = socketFactory;
            _networkManager = networkManager;
        }

        public string Name
        {
            get { return "Legacy HD Homerun"; }
        }

        public override string Type
        {
            get { return DeviceType; }
        }

        public static string DeviceType
        {
            get { return "legacyhdhomerun"; }
        }

        private const string ChannelIdPrefix = "legacyhdhr_";

        private string GetChannelId(TunerHostInfo info, LegacyChannels i)
        {
            var id = ChannelIdPrefix + i.GuideNumber;

            id += '_' + (i.GuideName ?? string.Empty).GetMD5().ToString("N");

            return id;
        }

        private async Task<IEnumerable<LegacyChannels>> GetLineup(TunerHostInfo info, CancellationToken cancellationToken)
        {
            using (var stream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = string.Format("{0}/discover.json", GetApiUrl(info, false)),
                CancellationToken = CancellationToken.None,
                BufferContent = false
            }))
            {
                var response = JsonSerializer.DeserializeFromStream<LegacyDiscoverResponse>(stream);

                var options = new HttpRequestOptions
                {
                    Url = response.LineupURL,
                    CancellationToken = cancellationToken,
                    BufferContent = false
                };
                using (var httpstream = await _httpClient.Get(options).ConfigureAwait(false))
                {
                    var lineup = JsonSerializer.DeserializeFromStream<List<LegacyChannels>>(httpstream) ?? new List<LegacyChannels>();

                    _channelUrlMap.Clear();
                    foreach(LegacyChannels channel in lineup)
                        _channelUrlMap.Add(channel.GuideNumber, channel.URL);

                    return lineup.ToList();
                }
            }
        }

        protected override async Task<IEnumerable<ChannelInfo>> GetChannelsInternal(TunerHostInfo info, CancellationToken cancellationToken)
        {
            var lineup = await GetLineup(info, cancellationToken).ConfigureAwait(false);

            return lineup.Select(i => new ChannelInfo
            {
                Name = i.GuideName,
                Number = i.GuideNumber,
                Id = GetChannelId(info, i),
                TunerHostId = info.Id,
                VideoCodec = "MPEG2",
                AudioCodec = "AC3",
                ChannelType = ChannelType.TV
            });
        }

        private readonly Dictionary<string, LegacyDiscoverResponse> _modelCache = new Dictionary<string, LegacyDiscoverResponse>();
        private async Task<string> GetModelInfo(TunerHostInfo info, CancellationToken cancellationToken)
        {
            lock (_modelCache)
            {
                LegacyDiscoverResponse response;
                if (_modelCache.TryGetValue(info.Url, out response))
                {
                    return response.ModelNumber;
                }
            }

            try
            {
                using (var stream = await _httpClient.Get(new HttpRequestOptions()
                {
                    Url = string.Format("{0}/discover.json", GetApiUrl(info, false)),
                    CancellationToken = cancellationToken,
                    CacheLength = TimeSpan.FromDays(1),
                    CacheMode = CacheMode.Unconditional,
                    TimeoutMs = Convert.ToInt32(TimeSpan.FromSeconds(5).TotalMilliseconds),
                    BufferContent = false
                }).ConfigureAwait(false))
                {
                    var response = JsonSerializer.DeserializeFromStream<LegacyDiscoverResponse>(stream);

                    lock (_modelCache)
                    {
                        _modelCache[info.Id] = response;
                    }

                    return response.ModelNumber;
                }
            }
            catch (HttpException ex)
            {
                if (ex.StatusCode.HasValue && ex.StatusCode.Value == System.Net.HttpStatusCode.NotFound)
                {
                    var defaultValue = "HDHR";
                    // HDHR4 doesn't have this api
                    lock (_modelCache)
                    {
                        _modelCache[info.Id] = new LegacyDiscoverResponse
                        {
                            ModelNumber = defaultValue
                        };
                    }
                    return defaultValue;
                }

                throw;
            }
        }

        public async Task<List<LiveTvTunerInfo>> GetTunerInfos(TunerHostInfo info, CancellationToken cancellationToken)
        {
            var model = await GetModelInfo(info, cancellationToken).ConfigureAwait(false);
            var tuners = new List<LiveTvTunerInfo>();

            var legCommand = new LegacyHdHomerunCommand(_socketFactory);
            // Legacy HdHomeruns are IPv4 only
            var ipInfo = new IpAddressInfo(info.Url, IpAddressFamily.InterNetwork);

            for (int i = 0; i < info.Tuners; ++i)
            {
                var name = String.Format("Tuner {0}", i+1);
                var currentChannel = "none"; /// @todo Get current channel and map back to Station Id      
                var isAvailable = await legCommand.CheckTunerAvailability(ipInfo, i, cancellationToken).ConfigureAwait(false);
                LiveTvTunerStatus status = isAvailable ? LiveTvTunerStatus.Available : LiveTvTunerStatus.LiveTv;
                tuners.Add(new LiveTvTunerInfo
                {
                    Name = name,
                    SourceType = string.IsNullOrWhiteSpace(model) ? Name : model,
                    ProgramName = currentChannel,
                    Status = status
                });
            }
            return tuners;
        }

        public async Task<List<LiveTvTunerInfo>> GetTunerInfos(CancellationToken cancellationToken)
        {
            var list = new List<LiveTvTunerInfo>();

            foreach (var host in GetConfiguration().TunerHosts
                .Where(i => i.IsEnabled && string.Equals(i.Type, Type, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    list.AddRange(await GetTunerInfos(host, cancellationToken).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error getting tuner info", ex);
                }
            }

            return list;
        }

        private string GetApiUrl(TunerHostInfo info, bool isPlayback)
        {
            var url = info.Url;

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Invalid tuner info");
            }

            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
            }

            var uri = new Uri(url);

            if (isPlayback)
            {
                var builder = new UriBuilder(uri);
                builder.Port = 5004;
                uri = builder.Uri;
            }

            return uri.AbsoluteUri.TrimEnd('/');
        }

        private class LegacyChannels
        {
            public string GuideNumber { get; set; }
            public string GuideName { get; set; }
            public string URL { get; set; }
        }

        private async Task<MediaSourceInfo> GetMediaSource(TunerHostInfo info, string channelId)
        {
            int? width = null;
            int? height = null;
            bool isInterlaced = true;
            string videoCodec = "mpeg2";
            string audioCodec = "ac3";

            int? videoBitrate = null;
            int? audioBitrate = null;

            var channels = await GetChannels(info, true, CancellationToken.None).ConfigureAwait(false);
            var channel = channels.FirstOrDefault(i => string.Equals(i.Number, channelId, StringComparison.OrdinalIgnoreCase));
            if (channel != null)
            {
                if (string.IsNullOrWhiteSpace(videoCodec))
                {
                    videoCodec = channel.VideoCodec;
                }
                audioCodec = channel.AudioCodec;
            }

            // normalize
            if (string.Equals(videoCodec, "mpeg2", StringComparison.OrdinalIgnoreCase))
            {
                videoCodec = "mpeg2video";
            }

            string nal = null;

            var url = String.Format("{0}_{1}", info.Url, _networkManager.GetRandomUnusedUdpPort());
            var id = channelId;
            id += "_" + url.GetMD5().ToString("N");

            var mediaSource = new MediaSourceInfo
            {
                Path = url,
                Protocol = MediaProtocol.Udp,
                MediaStreams = new List<MediaStream>
                        {
                            new MediaStream
                            {
                                Type = MediaStreamType.Video,
                                // Set the index to -1 because we don't know the exact index of the video stream within the container
                                Index = -1,
                                IsInterlaced = isInterlaced,
                                Codec = videoCodec,
                                Width = width,
                                Height = height,
                                BitRate = videoBitrate,
                                NalLengthSize = nal

                            },
                            new MediaStream
                            {
                                Type = MediaStreamType.Audio,
                                // Set the index to -1 because we don't know the exact index of the audio stream within the container
                                Index = -1,
                                Codec = audioCodec,
                                BitRate = audioBitrate
                            }
                        },
                RequiresOpening = true,
                RequiresClosing = true,
                BufferMs = 0,
                Container = "ts",
                Id = id,
                SupportsDirectPlay = false,
                SupportsDirectStream = true,
                SupportsTranscoding = true,
                IsInfiniteStream = true
            };

            mediaSource.InferTotalBitrate();

            return mediaSource;
        }

        protected EncodingOptions GetEncodingOptions()
        {
            return Config.GetConfiguration<EncodingOptions>("encoding");
        }

        private string GetHdHrIdFromChannelId(string channelId)
        {
            return channelId.Split('_')[1];
        }

        protected override async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(TunerHostInfo info, string channelId, CancellationToken cancellationToken)
        {
            var list = new List<MediaSourceInfo>();

            if (!channelId.StartsWith(ChannelIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return list;
            }
            var hdhrId = GetHdHrIdFromChannelId(channelId);

            list.Add(await GetMediaSource(info, hdhrId).ConfigureAwait(false));

            return list;
        }

        protected override bool IsValidChannelId(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                throw new ArgumentNullException("channelId");
            }

            return channelId.StartsWith(ChannelIdPrefix, StringComparison.OrdinalIgnoreCase);
        }

        protected override async Task<LiveStream> GetChannelStream(TunerHostInfo info, string channelId, string streamId, CancellationToken cancellationToken)
        {
            var profile = streamId.Split('_')[0];

            Logger.Info("GetChannelStream: channel id: {0}. stream id: {1} profile: {2}", channelId, streamId, profile);

            if (!channelId.StartsWith(ChannelIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Channel not found");
            }
            var hdhrId = GetHdHrIdFromChannelId(channelId);

            var mediaSource = await GetMediaSource(info, hdhrId).ConfigureAwait(false);

            string channelUrl = String.Empty;
            if (!_channelUrlMap.TryGetValue(hdhrId, out channelUrl))
            {
                Logger.Error("Unable to find channel url for channel number: {0}", hdhrId);
            }

            var liveStream = new LegacyHdHomerunLiveStream(mediaSource, streamId, channelUrl, info.Tuners, _fileSystem, _httpClient, Logger, Config.ApplicationPaths, _appHost, _socketFactory);
            liveStream.EnableStreamSharing = true;
            return liveStream;
        }

        public async Task Validate(TunerHostInfo info)
        {
            if (!info.IsEnabled)
            {
                return;
            }

            lock (_modelCache)
            {
                _modelCache.Clear();
            }
            // Test it by pulling down the lineup
            using (var stream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = string.Format("{0}/discover.json", GetApiUrl(info, false)),
                CancellationToken = CancellationToken.None,
                BufferContent = false
            }).ConfigureAwait(false))
            {
                var response = JsonSerializer.DeserializeFromStream<LegacyDiscoverResponse>(stream);

                info.DeviceId = response.DeviceID;
                info.Tuners = response.TunerCount;
            }
        }

        protected override async Task<bool> IsAvailableInternal(TunerHostInfo tuner, string channelId, CancellationToken cancellationToken)
        {
            var info = await GetTunerInfos(tuner, cancellationToken).ConfigureAwait(false);

            return info.Any(i => i.Status == LiveTvTunerStatus.Available);
        }

        public class LegacyDiscoverResponse
        {
            public string FriendlyName { get; set; }
            public string ModelNumber { get; set; }
            public int Legacy { get; set; }
            public string FirmwareName { get; set; }
            public string FirmwareVersion { get; set; }
            public string DeviceID { get; set; }
            public string DeviceAuth { get; set; }
            public int TunerCount { get; set; }
            public string BaseURL { get; set; }
            public string LineupURL { get; set; }
        }
    }
}
