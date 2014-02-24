using System.Collections.Generic;
using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Transport
{

    public class ServiceAction
    {
        public string Name
        { get; set; }

        public List<Argument> ArgumentList
        { get; set; }


        public override string ToString()
        {
            return Name;
        }

        internal static ServiceAction FromXml(XElement container)
        {
            List<Argument> argumentList = new List<Argument>();

            foreach (XElement arg in container.Descendants(uPnpNamespaces.svc + "argument"))
                argumentList.Add(Argument.FromXml(arg));
                  
            
            return new ServiceAction
            {
                Name = (string)container.Element(uPnpNamespaces.svc + "name").Value,
                ArgumentList = argumentList
            };
        }
    }
}
