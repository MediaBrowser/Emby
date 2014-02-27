using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Entities
{

    public class uBaseObject 
    {
        public string Id
        { get; set; }

        public string ParentId
        { get; set; }

        public string Title
        { get; set; }

        public string SecondText
        { get; set; }

        private string _iconUrl;
        public string IconUrl
        {
            get
            {
                return _iconUrl;
            }
            set
            {
                _iconUrl = value;
            }
        }

        public string MetaData
        { get; set; }

        public string Url
        { get; set; }

        public string[] ProtocolInfo
        { get; set; }

        internal static uBaseObject Create(XElement container)
        {
            return new uBaseObject
            {
                Id = (string)container.Attribute(uPnpNamespaces.Id).Value,
                ParentId = (string)container.Attribute(uPnpNamespaces.ParentId).Value,
                Title = (string)container.Element(uPnpNamespaces.title).Value,
                IconUrl = (container.Element(uPnpNamespaces.Artwork) != null) ? container.Element(uPnpNamespaces.Artwork).Value : "",
                SecondText = "",                
                Url = (container.Element(uPnpNamespaces.Res) != null) ? (string)container.Element(uPnpNamespaces.Res).Value : "",
                ProtocolInfo = (container.Element(uPnpNamespaces.Res) != null && container.Element(uPnpNamespaces.Res).Attribute(uPnpNamespaces.ProtocolInfo) != null) ? container.Element(uPnpNamespaces.Res).Attribute(uPnpNamespaces.ProtocolInfo).Value.Split(':') : new string[4],
                MetaData = container.ToString()
            };
        }
  
    }
}
