﻿using System.Xml.Serialization;
using MediaBrowser.Model.Dlna;

namespace MediaBrowser.Dlna.Profiles
{
    [XmlRoot("Profile")]
    public class KodiProfile : DefaultProfile
    {
        public KodiProfile()
        {
            Name = "Kodi";

            MaxStreamingBitrate = 100000000;
            MaxStaticBitrate = 100000000;
            MusicStreamingTranscodingBitrate = 1280000;
            MusicSyncBitrate = 1280000;

            TimelineOffsetSeconds = 5;

            Identification = new DeviceIdentification
            {
                ModelName = "Kodi",

                Headers = new[]
                {
                    new HttpHeaderInfo {Name = "User-Agent", Value = "Kodi", Match = HeaderMatchType.Substring}
                }
            };

            TranscodingProfiles = new TranscodingProfile[] { };

            DirectPlayProfiles = new[]
            {
                new DirectPlayProfile
                {
                    Container = "",
                    Type = DlnaProfileType.Video
                },

                new DirectPlayProfile
                {
                    Container = "",
                    Type = DlnaProfileType.Audio
                },

                new DirectPlayProfile
                {
                    Container = "",
                    Type = DlnaProfileType.Photo,
                }
            };

            ResponseProfiles = new ResponseProfile[] { };

            ContainerProfiles = new ContainerProfile[] { };

            CodecProfiles = new CodecProfile[] { };

            SubtitleProfiles = new []
            {
                new SubtitleProfile
                {
                    Format = "srt",
                    Method = SubtitleDeliveryMethod.External,
                },

                new SubtitleProfile
                {
                    Format = "sub",
                    Method = SubtitleDeliveryMethod.External,
                }
            };
        }
    }
}