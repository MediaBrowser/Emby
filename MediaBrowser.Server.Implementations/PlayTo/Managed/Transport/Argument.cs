using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Transport
{  
    public class Argument
    {
      
        public string Name
        { get; internal set; }

        public string Direction
        { get; internal set; }

        public string RelatedStateVariable
        { get; internal set; }

        internal static Argument FromXml(XElement container)
        {
            return new Argument
            {
                Name = (string)container.Element(uPnpNamespaces.svc + "name").Value,
                Direction = (string)container.Element(uPnpNamespaces.svc + "direction").Value,
                RelatedStateVariable = (string)container.Element(uPnpNamespaces.svc + "relatedStateVariable").Value                
            };
        }
    }
}
