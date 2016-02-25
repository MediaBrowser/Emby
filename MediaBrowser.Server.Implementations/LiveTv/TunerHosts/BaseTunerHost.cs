using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Server.Implementations.LiveTv.TunerHosts
{
    public abstract class BaseTunerHost
    {
        protected readonly IConfigurationManager Config;
        protected readonly ILogger Logger;
        protected IJsonSerializer JsonSerializer;
        protected readonly IMediaEncoder MediaEncoder;


        protected BaseTunerHost(IConfigurationManager config, ILogger logger, IJsonSerializer jsonSerializer, IMediaEncoder mediaEncoder)
        {
            Config = config;
            Logger = logger;
            JsonSerializer = jsonSerializer;
            MediaEncoder = mediaEncoder;
        }

        protected abstract Task<IEnumerable<ChannelInfo>> GetChannelsInternal(TunerHostInfo tuner, CancellationToken cancellationToken);
        public abstract string Type { get; }

        public async Task<IEnumerable<ChannelInfo>> GetChannels(TunerHostInfo tuner, bool enableCache, CancellationToken cancellationToken)
        {

            var result = await GetChannelsInternal(tuner, cancellationToken).ConfigureAwait(false);
            var list = result.ToList();
            Logger.Debug("Channels from {0}: {1}", tuner.Url, JsonSerializer.SerializeToString(list));

            return list;
        }

        protected virtual List<TunerHostInfo> GetTunerHosts()
        {
            return GetConfiguration().TunerHosts
                .Where(i => i.IsEnabled && string.Equals(i.Type, Type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<IEnumerable<ChannelInfo>> GetChannels(CancellationToken cancellationToken)
        {
            var list = new List<ChannelInfo>();

            var hosts = GetTunerHosts();

            foreach (var host in hosts)
            {
                try
                {
                    var channels = await GetChannels(host, true, cancellationToken).ConfigureAwait(false);
                    var newChannels = channels.Where(i => !list.Any(l => string.Equals(i.Id, l.Id, StringComparison.OrdinalIgnoreCase))).ToList();
                    newChannels.ForEach(c => {
                        c.ListingsProviderId = host.ListingsProvider ?? string.Empty;
                        c.Sources = new List<string> { host.Id};
                    });
                    list.AddRange(newChannels);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error getting channel list", ex);
                }
            }

            return list;
        }

        protected abstract Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(TunerHostInfo tuner, string channelId, CancellationToken cancellationToken);

        public async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(ChannelInfo channel, CancellationToken cancellationToken)
        {
            var channelId = channel.Id;
            if (IsValidChannelId(channelId))
            {
                foreach (var host in GetTunerHosts())
                {
                    if (!channel.Sources.Any(i => string.Equals(i, host.Id, StringComparison.OrdinalIgnoreCase))) { continue; }

                    var resourcePool = GetLock(host.Url);
                    Logger.Debug("GetChannelStreamMediaSources - Waiting on tuner resource pool");

                    await resourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);
                    Logger.Debug("GetChannelStreamMediaSources - Unlocked resource pool");

                    try
                    {
                        // Check to make sure the tuner is available
                        if (!await IsAvailable(host, channelId, cancellationToken).ConfigureAwait(false))
                        {
                            Logger.Error("Tuner is not currently available");
                            continue;
                        }

                        var mediaSources = await GetChannelStreamMediaSources(host, channelId, cancellationToken).ConfigureAwait(false);

                        // Prefix the id with the host Id so that we can easily find it
                        foreach (var mediaSource in mediaSources)
                        {
                            mediaSource.Id = host.Id + mediaSource.Id;
                        }

                        return mediaSources;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error opening tuner", ex);
                    }
                    finally
                    {
                        resourcePool.Release();
                    }
                }
            }

            return new List<MediaSourceInfo>();
        }

        protected abstract Task<MediaSourceInfo> GetChannelStream(TunerHostInfo tuner, string channelId, string streamId, CancellationToken cancellationToken);

        public async Task<Tuple<MediaSourceInfo, SemaphoreSlim>> GetChannelStream(ChannelInfo channel, string streamId, CancellationToken cancellationToken)
        {
            var channelId = channel.Id;
            if (IsValidChannelId(channelId))
            {
                foreach (var host in GetTunerHosts())
                {
                    if (string.IsNullOrWhiteSpace(streamId))
                    {
                        if (!channel.Sources.Any(i => string.Equals(i, host.Id, StringComparison.OrdinalIgnoreCase))){ continue; }
                    }
                    else if (streamId.StartsWith(host.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        streamId = streamId.Substring(host.Id.Length);
                    }
                    else { continue; }

                    var resourcePool = GetLock(host.Url);
                    Logger.Debug("GetChannelStream - Waiting on tuner resource pool");
                    await resourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);
                    Logger.Debug("GetChannelStream - Unlocked resource pool");
                    try
                    {
                        // Check to make sure the tuner is available                   
                        // If a streamId is specified then availibility has already been checked in GetChannelStreamMediaSources
                        if (string.IsNullOrWhiteSpace(streamId))
                        {
                            if (!await IsAvailable(host, channelId, cancellationToken).ConfigureAwait(false))
                            {
                                Logger.Error("Tuner is not currently available");
                                resourcePool.Release();
                                continue;
                            }
                        }

                        var stream = await GetChannelStream(host, channelId, streamId, cancellationToken).ConfigureAwait(false);

                        //await AddMediaInfo(stream, false, resourcePool, cancellationToken).ConfigureAwait(false);
                        return new Tuple<MediaSourceInfo, SemaphoreSlim>(stream, resourcePool);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error opening tuner", ex);

                        resourcePool.Release();
                    }
                }
            }

            throw new LiveTvConflictException();
        }

        protected async Task<bool> IsAvailable(TunerHostInfo tuner, string channelId, CancellationToken cancellationToken)
        {
            try
            {
                return await IsAvailableInternal(tuner, channelId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error checking tuner availability", ex);
                return false;
            }
        }

        protected abstract Task<bool> IsAvailableInternal(TunerHostInfo tuner, string channelId, CancellationToken cancellationToken);

        /// <summary>
        /// The _semaphoreLocks
        /// </summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Gets the lock.
        /// </summary>
        /// <param name="url">The filename.</param>
        /// <returns>System.Object.</returns>
        private SemaphoreSlim GetLock(string url)
        {
            return _semaphoreLocks.GetOrAdd(url, key => new SemaphoreSlim(1, 1));
        }

        private async Task AddMediaInfoInternal(MediaSourceInfo mediaSource, bool isAudio, CancellationToken cancellationToken)
        {
            var originalRuntime = mediaSource.RunTimeTicks;

            var info = await MediaEncoder.GetMediaInfo(new MediaInfoRequest
            {
                InputPath = mediaSource.Path,
                Protocol = mediaSource.Protocol,
                MediaType = isAudio ? DlnaProfileType.Audio : DlnaProfileType.Video,
                ExtractChapters = false

            }, cancellationToken).ConfigureAwait(false);

            mediaSource.Bitrate = info.Bitrate;
            mediaSource.Container = info.Container;
            mediaSource.Formats = info.Formats;
            mediaSource.MediaStreams = info.MediaStreams;
            mediaSource.RunTimeTicks = info.RunTimeTicks;
            mediaSource.Size = info.Size;
            mediaSource.Timestamp = info.Timestamp;
            mediaSource.Video3DFormat = info.Video3DFormat;
            mediaSource.VideoType = info.VideoType;

            mediaSource.DefaultSubtitleStreamIndex = null;

            // Null this out so that it will be treated like a live stream
            if (!originalRuntime.HasValue)
            {
                mediaSource.RunTimeTicks = null;
            }

            var audioStream = mediaSource.MediaStreams.FirstOrDefault(i => i.Type == Model.Entities.MediaStreamType.Audio);

            if (audioStream == null || audioStream.Index == -1)
            {
                mediaSource.DefaultAudioStreamIndex = null;
            }
            else
            {
                mediaSource.DefaultAudioStreamIndex = audioStream.Index;
            }

            var videoStream = mediaSource.MediaStreams.FirstOrDefault(i => i.Type == Model.Entities.MediaStreamType.Video);
            if (videoStream != null)
            {
                if (!videoStream.BitRate.HasValue)
                {
                    var width = videoStream.Width ?? 1920;

                    if (width >= 1900)
                    {
                        videoStream.BitRate = 8000000;
                    }

                    else if (width >= 1260)
                    {
                        videoStream.BitRate = 3000000;
                    }

                    else if (width >= 700)
                    {
                        videoStream.BitRate = 1000000;
                    }
                }
            }

            // Try to estimate this
            if (!mediaSource.Bitrate.HasValue)
            {
                var total = mediaSource.MediaStreams.Select(i => i.BitRate ?? 0).Sum();

                if (total > 0)
                {
                    mediaSource.Bitrate = total;
                }
            }
        }

        protected abstract bool IsValidChannelId(string channelId);

        protected LiveTvOptions GetConfiguration()
        {
            return Config.GetConfiguration<LiveTvOptions>("livetv");
        }

    }
}
