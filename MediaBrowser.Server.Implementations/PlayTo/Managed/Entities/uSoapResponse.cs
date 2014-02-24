using System.IO;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Entities
{
    internal class uSoapResponse
    {
        internal string StatusCode
        { get; set; }

        internal Stream Stream
        { get; set; }
    }
}
