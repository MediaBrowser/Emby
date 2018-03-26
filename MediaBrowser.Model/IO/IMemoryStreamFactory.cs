using System.IO;

namespace MediaBrowser.Model.IO
{
    public interface IMemoryStreamFactory
    {
        MemoryStream CreateNew();
    }
}
