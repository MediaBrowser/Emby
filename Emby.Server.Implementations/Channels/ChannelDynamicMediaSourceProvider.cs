using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Server.Implementations.Channels
{
    public class ChannelDynamicMediaSourceProvider : IMediaSourceProvider
    {
        private readonly ChannelManager _channelManager;

        public ChannelDynamicMediaSourceProvider(IChannelManager channelManager)
        {
            _channelManager = (ChannelManager)channelManager;
        }

        public async Task<IEnumerable<MediaSourceInfo>> GetMediaSources(IHasMediaSources item, CancellationToken cancellationToken)
        {
            var baseItem = (BaseItem) item;

            if (baseItem.SourceType == SourceType.Channel)
            {
                var result = await _channelManager.GetDynamicMediaSources(baseItem, cancellationToken).ConfigureAwait(false);

                foreach (var info in result)
                {
                    // There could be hls streams here and this isn't supported via direct stream through the server
                    info.SupportsDirectStream = false;
                }

                return result;
            }

            return new List<MediaSourceInfo>();
        }

        public Task<Tuple<MediaSourceInfo, IDirectStreamProvider>> OpenMediaSource(string openToken, bool allowLiveStreamProbe, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task CloseMediaSource(string liveStreamId)
        {
            throw new NotImplementedException();
        }
    }
}
