﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Users;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MediaBrowser.Controller.Channels
{
    public class ChannelAudioItem : Audio, IChannelMediaItem
    {
        public string ExternalId { get; set; }

        public string DataVersion { get; set; }

        public ChannelItemType ChannelItemType { get; set; }

        public bool IsInfiniteStream { get; set; }

        public ChannelMediaContentType ContentType { get; set; }

        public string OriginalImageUrl { get; set; }

        public List<ChannelMediaInfo> ChannelMediaSources { get; set; }

        protected override bool GetBlockUnratedValue(UserPolicy config)
        {
            return config.BlockUnratedItems.Contains(UnratedItem.ChannelContent);
        }

        protected override string CreateUserDataKey()
        {
            return ExternalId;
        }

        public override bool SupportsLocalMetadata
        {
            get
            {
                return false;
            }
        }

        public override bool IsSaveLocalMetadataEnabled()
        {
            return false;
        }

        public ChannelAudioItem()
        {
            ChannelMediaSources = new List<ChannelMediaInfo>();
        }

        public override LocationType LocationType
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                {
                    return LocationType.Remote;
                }
                
                return base.LocationType;
            }
        }

        protected override string GetInternalMetadataPath(string basePath)
        {
            return System.IO.Path.Combine(basePath, "channels", ChannelId, Id.ToString("N"));
        }

        public override IEnumerable<MediaSourceInfo> GetMediaSources(bool enablePathSubstitution)
        {
            var sources = ChannelManager.GetStaticMediaSources(this, false, CancellationToken.None)
                       .Result.ToList();

            if (sources.Count > 0)
            {
                return sources;
            }

            var list = base.GetMediaSources(enablePathSubstitution).ToList();

            foreach (var mediaSource in list)
            {
                if (string.IsNullOrWhiteSpace(mediaSource.Path))
                {
                    mediaSource.Type = MediaSourceType.Placeholder;
                }
            }

            return list;
        }

        public override bool CanDelete()
        {
            return false;
        }

        public override bool IsVisibleStandalone(User user)
        {
            return IsVisibleStandaloneInternal(user, false) && ChannelVideoItem.IsChannelVisible(this, user);
        }
    }
}
