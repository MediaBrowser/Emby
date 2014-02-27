using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Entities
{
    public class uContainer : uBaseObject
    {
        new internal static uBaseObject Create(XElement container)
        {
            return new uBaseObject
            {
                Id = (string)container.Attribute(uPnpNamespaces.Id),
                ParentId = (string)container.Attribute(uPnpNamespaces.ParentId),
                Title = (string)container.Element(uPnpNamespaces.title),
                IconUrl = (container.Element(uPnpNamespaces.Artwork) != null) ? container.Element(uPnpNamespaces.Artwork).Value : ""
            };
        }
    }
}
