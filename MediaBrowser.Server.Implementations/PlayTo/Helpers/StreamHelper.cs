using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Server.Implementations.PlayTo.Managed;
using MediaBrowser.Server.Implementations.PlayTo.Managed.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Helpers
{
    class StreamHelper
    {
        /// <summary>
        /// Gets the dlna headers.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        internal static string GetDlnaHeaders(PlaylistItem item)
        {
            var orgOp = item.Transcode ? ";DLNA.ORG_OP=00" : ";DLNA.ORG_OP=01";
            
            var orgCi = item.Transcode ? ";DLNA.ORG_CI=0" : ";DLNA.ORG_CI=1";

            const string dlnaflags = ";DLNA.ORG_FLAGS=01500000000000000000000000000000";

            var contentFeatures = string.Empty;

            if (string.Equals(item.FileFormat, "mp3", StringComparison.OrdinalIgnoreCase))
            {
                contentFeatures = "DLNA.ORG_PN=MP3";
            }           
            else if (string.Equals(item.FileFormat, "wma", StringComparison.OrdinalIgnoreCase))
            {
                contentFeatures = "DLNA.ORG_PN=WMABASE";
            }
            else if (string.Equals(item.FileFormat, "avi", StringComparison.OrdinalIgnoreCase))
            {
                contentFeatures = "DLNA.ORG_PN=AVI";
            }
            else if (string.Equals(item.FileFormat, "mkv", StringComparison.OrdinalIgnoreCase))
            {
                contentFeatures = "DLNA.ORG_PN=MATROSKA";
            }
            else if (string.Equals(item.FileFormat, "mp4", StringComparison.OrdinalIgnoreCase))
            {
                contentFeatures = "DLNA.ORG_PN=AVC_MP4_MP_HD_720p_AAC";
            }
            else if (string.Equals(item.FileFormat, "mpeg", StringComparison.OrdinalIgnoreCase))
            {
                contentFeatures = "DLNA.ORG_PN=MPEG_PS_PAL";
            }
            else if (string.Equals(item.FileFormat, "ts", StringComparison.OrdinalIgnoreCase))
            {
                contentFeatures = "DLNA.ORG_PN=MPEG_PS_PAL";
            }
            else if (item.IsVideo)
            {
                //Default to AVI for video
                contentFeatures = "DLNA.ORG_PN=AVI";
            }
            else
            {
                //Default to MP3 for audio
                contentFeatures = "DLNA.ORG_PN=MP3";
            }

            return(contentFeatures + orgOp + orgCi + dlnaflags).Trim(';');            
        }

        #region Audio

        /// <summary>
        /// Gets the audio URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="ip">The server ip.</param>
        /// <param name="port">The server port.</param>
        /// <returns></returns>
        internal static string GetAudioUrl(PlaylistItem item, string ip, int port)
        {
            if (!item.Transcode)
                return string.Format("http://{0}:{1}/mediabrowser/audio/{2}/stream.{3}?Static=True", ip, port, item.ItemId, item.FileFormat);
            else
                //Always use Mp3 codec when transcoding until we have a better ProfileConfiguration
                return string.Format("http://{0}:{1}/mediabrowser/audio/{2}/stream.mp3?AudioCodec=Mp3", ip, port, item.ItemId);

        }

        #endregion

        #region Video

        /// <summary>
        /// Gets the video URL.
        /// </summary>
        /// <param name="deviceProperties">The device properties.</param>
        /// <param name="item">The item.</param>
        /// <param name="streams">The streams.</param>
        /// <param name="serverip">The serverip.</param>
        /// <param name="serverport">The serverport.</param>
        /// <returns>The url to send to the device</returns>
        internal static string GetVideoUrl(DeviceProperties deviceProperties, PlaylistItem item, IEnumerable<MediaStream> streams, string serverip, int serverport)
        {
            if (!item.Transcode)
                return string.Format("http://{0}:{1}/mediabrowser/Videos/{2}/stream.{3}?Static=True", serverip, serverport, item.ItemId, item.FileFormat);
            else
            {
                var videostream = streams.Where(m => m.Type == MediaStreamType.Video).OrderBy(m => m.IsDefault).FirstOrDefault();
                var audiostream = streams.Where(m => m.Type == MediaStreamType.Audio).OrderBy(m => m.IsDefault).FirstOrDefault();

                var videoCodec = GetVideoCodec(videostream);
                var audioCodec = GetAudioCodec(audiostream);
                int? videoBitrate = null;
                int? audioBitrate = null;
                int? audioChannels = null;

                if (videoCodec != VideoCodecs.Copy)
                    videoBitrate = 2000000;

                if (audioCodec != AudioCodecs.Copy)
                {
                    audioBitrate = 128000;
                    audioChannels = 2;
                }

                string dlnaCommand = BuildDlnaUrl(deviceProperties.UUID, videoCodec, audioCodec, null, null, videoBitrate, audioChannels, audioBitrate, item.StartPositionTicks, "baseline", "3");
                return string.Format("http://{0}:{1}/mediabrowser/Videos/{2}/stream.{3}?{4}", serverip, serverport, item.ItemId, item.FileFormat, dlnaCommand);
            }
        }

        /// <summary>
        /// Gets the video codec.
        /// </summary>
        /// <param name="videoStream">The video stream.</param>
        /// <returns></returns>
        private static VideoCodecs GetVideoCodec(MediaStream videoStream)
        {
            switch (videoStream.Codec.ToLower())
            {
                case "h264":
                case "mpeg4":
                    return VideoCodecs.Copy;

            }
            return VideoCodecs.H264;
        }

        /// <summary>
        /// Gets the audio codec.
        /// </summary>
        /// <param name="audioStream">The audio stream.</param>
        /// <returns></returns>
        private static AudioCodecs GetAudioCodec(MediaStream audioStream)
        {
            if (audioStream != null)
            {
                switch (audioStream.Codec.ToLower())
                {
                    case "aac":
                    case "mp3":
                    case "wma":
                        return AudioCodecs.Copy;

                }
            }
            return AudioCodecs.Aac;
        }

        /// <summary>
        /// Builds the dlna URL.
        /// </summary>
        /// <param name="deviceID">The device identifier.</param>
        /// <param name="videoCodec">The video codec.</param>
        /// <param name="audioCodec">The audio codec.</param>
        /// <param name="subtitleIndex">Index of the subtitle.</param>
        /// <param name="audiostreamIndex">Index of the audiostream.</param>
        /// <param name="videoBitrate">The video bitrate.</param>
        /// <param name="audiochannels">The audiochannels.</param>
        /// <param name="audioBitrate">The audio bitrate.</param>
        /// <param name="startPositionTicks">The start position ticks.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="videoLevel">The video level.</param>
        /// <returns>A Dlna device compatible url</returns>
        private static string BuildDlnaUrl(string deviceID, VideoCodecs? videoCodec, AudioCodecs? audioCodec, int? subtitleIndex, int? audiostreamIndex, int? videoBitrate, int? audiochannels, int? audioBitrate, long? startPositionTicks, string profile, string videoLevel)
        {
            string dlnaparam = string.Format("Params={0};", deviceID);

            dlnaparam += videoCodec.HasValue ? videoCodec.Value.ToString() + ";" : ";";
            dlnaparam += audioCodec.HasValue ? audioCodec.Value.ToString() + ";" : ";";
            dlnaparam += audiostreamIndex.HasValue ? audiostreamIndex.Value.ToString() + ";" : ";";
            dlnaparam += subtitleIndex.HasValue ? subtitleIndex.Value.ToString() + ";" : ";";
            dlnaparam += videoBitrate.HasValue ? videoBitrate.Value.ToString() + ";" : ";";
            dlnaparam += audioBitrate.HasValue ? audioBitrate.Value.ToString() + ";" : ";";
            dlnaparam += audiochannels.HasValue ? audiochannels.Value.ToString() + ";" : ";";
            dlnaparam += startPositionTicks.HasValue ? startPositionTicks.Value.ToString() + ";" : ";";
            dlnaparam += profile + ";";
            dlnaparam += videoLevel + ";";


            return dlnaparam;
        }

        #endregion        

    }
}
