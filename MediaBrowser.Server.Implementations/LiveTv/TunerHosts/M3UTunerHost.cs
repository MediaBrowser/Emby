using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Server.Implementations.LiveTv.TunerHosts
{
    public class M3UTunerHost : BaseTunerHost, ITunerHost, IConfigurableTunerHost
    {
        private readonly IFileSystem _fileSystem;
        private readonly IHttpClient _httpClient;

        public M3UTunerHost(IConfigurationManager config, ILogger logger, IJsonSerializer jsonSerializer, IMediaEncoder mediaEncoder, IFileSystem fileSystem, IHttpClient httpClient)
            : base(config, logger, jsonSerializer, mediaEncoder)
        {
            _fileSystem = fileSystem;
            _httpClient = httpClient;
        }

        public override string Type
        {
            get { return "m3u"; }
        }

        public string Name
        {
            get { return "M3U Tuner"; }
        }

        private const string ChannelIdPrefix = "m3u_";

        protected override async Task<IEnumerable<ChannelInfo>> GetChannelsInternal(TunerHostInfo info, CancellationToken cancellationToken)
        {
            return await new M3uParser(Logger, _fileSystem, _httpClient).Parse(info.Url, ChannelIdPrefix, cancellationToken).ConfigureAwait(false);
        }

        public Task<List<LiveTvTunerInfo>> GetTunerInfos(CancellationToken cancellationToken)
        {
            var list = GetTunerHosts()
            .Select(i => new LiveTvTunerInfo()
            {
                Name = Name,
                SourceType = Type,
                Status = LiveTvTunerStatus.Available,
                Id = i.Url.GetMD5().ToString("N"),
                Url = i.Url
            })
            .ToList();

            return Task.FromResult(list);
        }

        protected override async Task<MediaSourceInfo> GetChannelStream(TunerHostInfo info, string path, string streamId, CancellationToken cancellationToken)
        {
            var sources = await GetChannelStreamMediaSources(info, path, cancellationToken).ConfigureAwait(false);

            return sources.First();
        }

        public async Task Validate(TunerHostInfo info)
        {
            using (var stream = await new M3uParser(Logger, _fileSystem, _httpClient).GetListingsStream(info.Url, CancellationToken.None).ConfigureAwait(false))
            {

            }
        }

        protected override async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(TunerHostInfo info, string path, CancellationToken cancellationToken)
        {
            MediaProtocol protocol = MediaProtocol.File;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                protocol = MediaProtocol.Http;
            }
            else if (path.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase))
            {
                protocol = MediaProtocol.Rtmp;
            }
            else if (path.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase))
            {
                protocol = MediaProtocol.Rtsp;
            }

            var mediaSource = new MediaSourceInfo
            {
                Path = path,
                Protocol = protocol,
                MediaStreams = new List<MediaStream>
                    {
                        new MediaStream
                        {
                            Type = MediaStreamType.Video,
                            // Set the index to -1 because we don't know the exact index of the video stream within the container
                            Index = -1,
                            IsInterlaced = true
                        },
                        new MediaStream
                        {
                            Type = MediaStreamType.Audio,
                            // Set the index to -1 because we don't know the exact index of the audio stream within the container
                            Index = -1

                        }
                    },
                RequiresOpening = false,
                RequiresClosing = false
            };

            return new List<MediaSourceInfo> { mediaSource };
        }

        protected override Task<bool> IsAvailableInternal(TunerHostInfo tuner, string channelId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}