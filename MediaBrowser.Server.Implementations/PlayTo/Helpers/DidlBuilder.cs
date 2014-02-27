using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Helpers
{
    internal class DidlBuilder
    {
        #region Constants

        internal const string CRLF = "\r\n";
        internal const string UNKNOWN = "Unknown";

        internal const string DIDL_START =      @"<item id=""{0}"" parentID=""{1}"" restricted=""1"" xmlns=""urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/"">" + CRLF;
        internal const string DIDL_TITLE =      @"  <dc:title xmlns:dc=""http://purl.org/dc/elements/1.1/"">{0}</dc:title>" + CRLF;
        internal const string DIDL_ARTIST =     @"<upnp:artist xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">{0}</upnp:artist>" + CRLF;
        internal const string DIDL_ALBUM =      @"<upnp:album xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">{0}</upnp:album>" + CRLF;
        internal const string DIDL_TRACKNUM =   @"<upnp:originalTrackNumber xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">0</upnp:originalTrackNumber>" + CRLF;
        internal const string DIDL_VIDEOCLASS = @"  <upnp:class xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">object.item.videoItem</upnp:class>" + CRLF;
        internal const string DIDL_AUDIOCLASS = @"  <upnp:class xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">object.item.audioItem.musicTrack</upnp:class>" + CRLF;
        internal const string DIDL_IMAGE =      @"  <upnp:albumArtURI dlna:profileID=""JPEG_TN"" xmlns:dlna=""urn:schemas-dlna-org:metadata-1-0/"" xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">{0}</upnp:albumArtURI>" + CRLF +
                                                @"  <upnp:icon xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">{0}</upnp:icon>" + CRLF;
        internal const string DIDL_RELEASEDATE = @"  <dc:date xmlns:dc=""http://purl.org/dc/elements/1.1/"">{0}</dc:date>" + CRLF;
        internal const string DIDL_GENRE =      @"  <upnp:genre xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">{0}</upnp:genre>" + CRLF;
        internal const string DESCRIPTION =     @"  <dc:description xmlns:dc=""http://purl.org/dc/elements/1.1/"">{0}</dc:description>" + CRLF;
        internal const string DIDL_VIDEO_RES =  @"  <res bitrate=""{0}"" duration=""{1}"" protocolInfo=""http-get:*:video/x-msvideo:DLNA.ORG_PN=AVI;DLNA.ORG_OP=01;DLNA.ORG_CI=0;DLNA.ORG_FLAGS=01500000000000000000000000000000"" resolution=""{2}x{3}"" size=""0"">{4}</res>" + CRLF;
        internal const string DIDL_AUDIO_RES =  @"  <res bitrate=""{0}"" duration=""{1}"" nrAudioChannels=""2"" protocolInfo=""http-get:*:audio/mp3:DLNA.ORG_OP=01;DLNA.ORG_CI=0;DLNA.ORG_FLAGS=01500000000000000000000000000000"" sampleFrequency=""{2}"" size=""0"">{3}</res>" + CRLF;
        internal const string DIDL_IMAGE_RES =  @"  <res protocolInfo=""http-get:*:image/jpeg:DLNA.ORG_PN=JPEG_TN;DLNA.ORG_OP=00;DLNA.ORG_CI=1;DLNA.ORG_FLAGS=00D00000000000000000000000000000"" resolution=""212x320"">{0}</res>" + CRLF;
        internal const string DIDL_ALBUMIMAGE_RES = @"  <res protocolInfo=""http-get:*:image/jpeg:DLNA.ORG_PN=JPEG_TN;DLNA.ORG_OP=00;DLNA.ORG_CI=1;DLNA.ORG_FLAGS=00D00000000000000000000000000000"" resolution=""320x320"">{0}</res>" + CRLF;
        internal const string DIDL_RATING =     @"  <upnp:rating xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">{0}</upnp:rating>" + CRLF;
        internal const string DIDL_END = "</item>";

        #endregion

        /// <summary>
        /// Builds a Didl MetaData object for the specified dto.
        /// </summary>
        /// <param name="dto">The dto.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="host">The server host.</param>
        /// <param name="port">The server port.</param>
        /// <param name="streamUrl">The stream URL.</param>
        /// <param name="streams">The streams.</param>
        /// <returns></returns>
        internal static string Build(BaseItem dto, string userId, string host, int port, string streamUrl, IEnumerable<MediaStream> streams)
        {
            string response = string.Format(DIDL_START, dto.Id, userId);
            response += string.Format(DIDL_TITLE, dto.Name.Replace("&", "and"));
            if (IsVideo(dto))
                response += DIDL_VIDEOCLASS;                            
            else
                response += DIDL_AUDIOCLASS;

            response += string.Format(DIDL_IMAGE, GetImageUrl(dto, host, port));
            response += string.Format(DIDL_RELEASEDATE, GetDateString(dto.PremiereDate));
            response += string.Format(DIDL_GENRE, UNKNOWN); //TODO Add genres to didl;
            if (IsVideo(dto))
            {
                response += string.Format(DESCRIPTION, UNKNOWN);
                response += GetVideoDIDL(dto, streamUrl, streams);
                response += string.Format(DIDL_IMAGE_RES, GetImageUrl(dto, host, port));
            }
            else
            {                
                response += string.Format(DIDL_ARTIST, ((Audio)dto).Artists.Any() ? ((Audio)dto).Artists.First(): UNKNOWN);
                response += string.Format(DIDL_ALBUM, ((Audio)dto).Album);
                response += string.Format(DIDL_TRACKNUM, ((Audio)dto).IndexNumber.HasValue ? ((Audio)dto).IndexNumber.Value : 0);
                response += GetAudioDIDL(dto, streamUrl, streams);
                response += string.Format(DIDL_ALBUMIMAGE_RES, GetImageUrl(dto, host, port));
            }
                        
            response += DIDL_END;

            return response;
             
        }

        #region Private methods

        private static string GetVideoDIDL(BaseItem dto, string streamUrl, IEnumerable<MediaStream> streams)
        {
            var videostream = streams.Where(stream => stream.Type == Model.Entities.MediaStreamType.Video).OrderBy(s => s.IsDefault).FirstOrDefault();
            return string.Format(DIDL_VIDEO_RES, videostream.BitRate.HasValue ? videostream.BitRate.Value / 10 : 0, GetDurationString(dto), videostream.Width, videostream.Height, streamUrl);
        }

        private static string GetAudioDIDL(BaseItem dto, string streamUrl, IEnumerable<MediaStream> streams)
        {
            var audiostream = streams.Where(stream => stream.Type == Model.Entities.MediaStreamType.Audio).OrderBy(s => s.IsDefault).FirstOrDefault();
            return string.Format(DIDL_AUDIO_RES, audiostream.BitRate.HasValue ? audiostream.BitRate.Value / 10 : 16000, GetDurationString(dto), audiostream.SampleRate.HasValue ? audiostream.SampleRate.Value : 0, streamUrl);
        }

        private static string GetImageUrl(BaseItem dto, string host, int port)
        {
            if (!dto.HasImage(Model.Entities.ImageType.Primary, 0))
                return string.Format("http://{0}:{1}/mediabrowser/Items/{2}/Images/Primary", host, port, dto.Parent.Id);
            return string.Format("http://{0}:{1}/mediabrowser/Items/{2}/Images/Primary", host, port, dto.Id);
        }

        private static string GetDurationString(BaseItem dto)
        {
            var duration = TimeSpan.FromTicks(dto.RunTimeTicks.HasValue ? dto.RunTimeTicks.Value : 0);
            return string.Format("{0}:{1:00}:2{00}.000", duration.Hours, duration.Minutes, duration.Seconds);
        }

        private static string GetDateString(DateTime? date)
        {
            if (!date.HasValue)
                return UNKNOWN;

            return string.Format("{0}-{1:00}-{2:00}", date.Value.Year, date.Value.Month, date.Value.Day);
        }

        private static bool IsVideo(BaseItem item)
        {
            return string.Equals(item.MediaType, MediaBrowser.Model.Entities.MediaType.Video, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
