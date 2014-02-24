using System.Linq;
using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Entities
{
    public class uIcon
    {                        
        private string _url;
        internal string Url
        {
            get
            {
                return _url;
            }
        }
        
        private string _mimeType;
        internal string MimeType
        {
            get
            {
                return _mimeType;
            }
        }
        
        private int _width;
        internal int Width
        {
            get
            {
                return _width;
            }
        }
        
        private int _height;
        internal int Height
        {
            get
            {
                return _height;
            }
        }
        
        private string _depth;
        internal string Depth
        {
            get
            {
                return _depth;
            }
        }

        internal uIcon(string mimeType, string width, string height, string depth, string url)
        {
            _mimeType = mimeType;
            _width = (!string.IsNullOrEmpty(width)) ? int.Parse(width) : 0;
            _height = (!string.IsNullOrEmpty(height)) ? int.Parse(height) : 0;
            _depth = depth;
            _url = url;
        }

        internal static uIcon FromXml(XElement element)
        {
            string mimeType = element.Descendants(uPnpNamespaces.ud.GetName("mimetype")).FirstOrDefault().Value;
            string width = element.Descendants(uPnpNamespaces.ud.GetName("width")).FirstOrDefault().Value;
            string height = element.Descendants(uPnpNamespaces.ud.GetName("height")).FirstOrDefault().Value;
            string depth = element.Descendants(uPnpNamespaces.ud.GetName("depth")).FirstOrDefault().Value;
            string url = element.Descendants(uPnpNamespaces.ud.GetName("url")).FirstOrDefault().Value;

            return new uIcon(mimeType, width, height, depth, url);
        }

        public override string ToString()
        {
            return string.Format("{0}x{1}", Height, Width);
        }
    }
}
