using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed
{
    internal class uPnpNamespaces
    {
        internal static XNamespace dc = "http://purl.org/dc/elements/1.1/";
        internal static XNamespace ns = "urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/";
        internal static XNamespace svc = "urn:schemas-upnp-org:service-1-0";
        internal static XNamespace ud = "urn:schemas-upnp-org:device-1-0";
        internal static XNamespace upnp = "urn:schemas-upnp-org:metadata-1-0/upnp/";
        internal static XNamespace RenderingControl = "urn:schemas-upnp-org:service:RenderingControl:1";
        internal static XNamespace AvTransport = "urn:schemas-upnp-org:service:AVTransport:1";
        internal static XNamespace ContentDirectory = "urn:schemas-upnp-org:service:ContentDirectory:1";

        internal static XName containers = ns + "container";
        internal static XName items = ns + "item";
        internal static XName title = dc + "title";
        internal static XName creator = dc + "creator";
        internal static XName artist = upnp + "artist";
        internal static XName Id = "id";
        internal static XName ParentId = "parentID";
        internal static XName uClass = upnp + "class";
        internal static XName Artwork = upnp + "albumArtURI";
        internal static XName Description = dc + "description";
        internal static XName LongDescription = upnp + "longDescription";
        internal static XName Album = upnp + "album";
        internal static XName Author = upnp + "author";
        internal static XName Director = upnp + "director";
        internal static XName PlayCount = upnp + "playbackCount";
        internal static XName Tracknumber = upnp + "originalTrackNumber";
        internal static XName Res = ns + "res";
        internal static XName Duration = "duration";
        internal static XName ProtocolInfo = "protocolInfo";

        internal static XName ServiceStateTable = svc + "serviceStateTable";
        internal static XName StateVariable = svc + "stateVariable";
    }
}
