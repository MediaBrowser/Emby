namespace MediaBrowser.Controller.LiveTv
{
    using System;
    using System.Collections.Generic;

    public class ChannelInfoDictionaries
    {
        private readonly Lazy<IDictionary<string, ChannelInfo>> _lazyChannelsByName;

        private readonly Lazy<IDictionary<string, ChannelInfo>> _lazyChannelsById;

        private readonly Lazy<IDictionary<string, ChannelInfo>> _lazyChannelsByNumber;

        public ChannelInfoDictionaries(List<ChannelInfo> channelInfos)
        {
            _lazyChannelsByName = new Lazy<IDictionary<string, ChannelInfo>>(
                () =>
                {
                    var dictionary = new Dictionary<string, ChannelInfo>();
                    foreach (var channelInfo in channelInfos)
                    {
                        dictionary[channelInfo.Name?.ToUpper() ?? string.Empty] = channelInfo;
                    }

                    return dictionary;
                });
            _lazyChannelsById = new Lazy<IDictionary<string, ChannelInfo>>(
                () =>
                {
                    var dictionary = new Dictionary<string, ChannelInfo>();
                    foreach (var channelInfo in channelInfos)
                    {
                        dictionary[channelInfo.Id?.ToUpper() ?? string.Empty] = channelInfo;
                    }

                    return dictionary;
                });
            _lazyChannelsByNumber = new Lazy<IDictionary<string, ChannelInfo>>(
                () =>
                {
                    var dictionary = new Dictionary<string, ChannelInfo>();
                    foreach (var channelInfo in channelInfos)
                    {
                        dictionary[NormalizeName(channelInfo.Name?.ToUpper() ?? string.Empty)] = channelInfo;
                    }

                    return dictionary;
                });
        }

        public IDictionary<string, ChannelInfo> ChannelsByName => _lazyChannelsByName.Value;

        public IDictionary<string, ChannelInfo> ChannelsById => _lazyChannelsById.Value;

        public IDictionary<string, ChannelInfo> ChannelsByNumber => _lazyChannelsByNumber.Value;

        private static string NormalizeName(string value)
        {
            return value.Replace(" ", string.Empty).Replace("-", string.Empty);
        }
    }
}