using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using System.Collections.Generic;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Configuration;
using System.IO;
using MediaBrowser.Common.Extensions;

namespace Emby.Server.Implementations.LiveTv
{
    public class LiveStreamHelper
    {
        private readonly IMediaEncoder _mediaEncoder;
        private readonly ILogger _logger;

        const int ProbeAnalyzeDurationMs = 3000;
        const int PlaybackAnalyzeDurationMs = 3000;
        private IJsonSerializer _json;
        private IApplicationPaths _appPaths;

        public LiveStreamHelper(IMediaEncoder mediaEncoder, ILogger logger, IJsonSerializer json, IApplicationPaths appPaths)
        {
            _mediaEncoder = mediaEncoder;
            _logger = logger;
            _json = json;
            _appPaths = appPaths;
        }

        public async Task AddMediaInfoWithProbe(MediaSourceInfo mediaSource, bool isAudio, string cacheKey, CancellationToken cancellationToken)
        {
            var originalRuntime = mediaSource.RunTimeTicks;

            var now = DateTime.UtcNow;

            MediaInfo mediaInfo = null;
            var cacheFilePath = string.IsNullOrWhiteSpace(cacheKey) ? null : Path.Combine(_appPaths.CachePath, "livetvmediainfo", cacheKey.GetMD5().ToString("N") + ".json");

            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                try
                {
                    mediaInfo = _json.DeserializeFromFile<MediaInfo>(cacheFilePath);

                    //_logger.Debug("Found cached media info");
                }
                catch (Exception ex)
                {
                }
            }

            if (mediaInfo == null)
            {
                var path = mediaSource.Path;
                var protocol = mediaSource.Protocol;

                if (!string.IsNullOrWhiteSpace(mediaSource.EncoderPath) && mediaSource.EncoderProtocol.HasValue)
                {
                    path = mediaSource.EncoderPath;
                    protocol = mediaSource.EncoderProtocol.Value;
                }

                mediaInfo = await _mediaEncoder.GetMediaInfo(new MediaInfoRequest
                {
                    InputPath = path,
                    Protocol = protocol,
                    MediaType = isAudio ? DlnaProfileType.Audio : DlnaProfileType.Video,
                    ExtractChapters = false,
                    AnalyzeDurationMs = ProbeAnalyzeDurationMs

                }, cancellationToken).ConfigureAwait(false);

                if (cacheFilePath != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath));
                    _json.SerializeToFile(mediaInfo, cacheFilePath);

                    //_logger.Debug("Saved media info to {0}", cacheFilePath);
                }
            }

            var mediaStreams = mediaInfo.MediaStreams;

            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                var newList = new List<MediaStream>();
                newList.AddRange(mediaStreams.Where(i => i.Type == MediaStreamType.Video).Take(1));
                newList.AddRange(mediaStreams.Where(i => i.Type == MediaStreamType.Audio).Take(1));

                foreach (var stream in newList)
                {
                    stream.Index = -1;
                    stream.Language = null;
                }

                mediaStreams = newList;
            }

            _logger.Info("Live tv media info probe took {0} seconds", (DateTime.UtcNow - now).TotalSeconds.ToString(CultureInfo.InvariantCulture));

            mediaSource.Bitrate = mediaInfo.Bitrate;
            mediaSource.Container = mediaInfo.Container;
            mediaSource.Formats = mediaInfo.Formats;
            mediaSource.MediaStreams = mediaStreams;
            mediaSource.RunTimeTicks = mediaInfo.RunTimeTicks;
            mediaSource.Size = mediaInfo.Size;
            mediaSource.Timestamp = mediaInfo.Timestamp;
            mediaSource.Video3DFormat = mediaInfo.Video3DFormat;
            mediaSource.VideoType = mediaInfo.VideoType;

            mediaSource.DefaultSubtitleStreamIndex = null;

            // Null this out so that it will be treated like a live stream
            if (!originalRuntime.HasValue)
            {
                mediaSource.RunTimeTicks = null;
            }

            var audioStream = mediaStreams.FirstOrDefault(i => i.Type == MediaBrowser.Model.Entities.MediaStreamType.Audio);

            if (audioStream == null || audioStream.Index == -1)
            {
                mediaSource.DefaultAudioStreamIndex = null;
            }
            else
            {
                mediaSource.DefaultAudioStreamIndex = audioStream.Index;
            }

            var videoStream = mediaStreams.FirstOrDefault(i => i.Type == MediaBrowser.Model.Entities.MediaStreamType.Video);
            if (videoStream != null)
            {
                if (!videoStream.BitRate.HasValue)
                {
                    var width = videoStream.Width ?? 1920;

                    if (width >= 3000)
                    {
                        videoStream.BitRate = 30000000;
                    }

                    else if (width >= 1900)
                    {
                        videoStream.BitRate = 20000000;
                    }

                    else if (width >= 1200)
                    {
                        videoStream.BitRate = 8000000;
                    }

                    else if (width >= 700)
                    {
                        videoStream.BitRate = 2000000;
                    }
                }

                // This is coming up false and preventing stream copy
                videoStream.IsAVC = null;
            }

            // Try to estimate this
            mediaSource.InferTotalBitrate(true);

            mediaSource.AnalyzeDurationMs = PlaybackAnalyzeDurationMs;
        }

        public Task AddMediaInfoWithProbe(MediaSourceInfo mediaSource, bool isAudio, CancellationToken cancellationToken)
        {
            return AddMediaInfoWithProbe(mediaSource, isAudio, null, cancellationToken);
        }
    }
}
