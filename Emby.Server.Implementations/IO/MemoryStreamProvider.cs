using System.IO;
using MediaBrowser.Model.IO;

namespace Emby.Server.Implementations.IO
{
    public class MemoryStreamProvider : IMemoryStreamFactory
    {
        public MemoryStream CreateNew()
        {
            return new MemoryStream();
        }
    }
}
