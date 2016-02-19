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

namespace MediaBrowser.Server.Implementations.LiveTv.Listings
{
    public abstract class BaseListingsProvider
    {
        protected readonly IConfigurationManager Config;
        protected readonly ILogger Logger;
        protected IJsonSerializer JsonSerializer;

        protected abstract Task<IEnumerable<ProgramInfo>> GetProgramsAsyncInternal(ListingsProviderInfo info, string channelNumber, string channelName, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken);

        public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(ListingsProviderInfo info, string channelNumber, string channelName, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            if (info.ChannelOffset > 0)
            {
                channelNumber = (-info.ChannelOffset + Convert.ToSingle(channelNumber)).ToString();
            }
            var result = await GetProgramsAsyncInternal(info, channelNumber, channelName, startDateUtc, endDateUtc, cancellationToken);
            return result;
;       }

        protected abstract Task AddMetadataInternal(ListingsProviderInfo info, List<ChannelInfo> channels, CancellationToken cancellationToken);

        public async Task AddMetadata(ListingsProviderInfo info, List<ChannelInfo> channels, CancellationToken cancellationToken)
        {
            if (info.ChannelOffset > 0)
            {
                foreach (var channel in channels)
                {
                    float channelInt = Convert.ToSingle(channel.Number) - info.ChannelOffset;
                    channel.Number = channelInt.ToString();
                }
            }
            await AddMetadataInternal(info, channels, cancellationToken);
            if (info.ChannelOffset > 0)
            {
                foreach (var channel in channels)
                {
                    float channelInt = Convert.ToSingle(channel.Number) + info.ChannelOffset;
                    channel.Number = channelInt.ToString();
                }
            }
        }
    }
}
