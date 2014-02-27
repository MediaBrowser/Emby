using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Entities
{
    internal class uParser
    {
        public static IList<uBaseObject> ParseBrowseXml(XDocument doc)
        {
            List<uBaseObject> list = new List<uBaseObject>();

            var item = (from result in doc.Document.Descendants("Result") select result).FirstOrDefault();

            if (item == null)
                return list;

            XElement uPnpResponse = XElement.Parse((String)item);

            var uObjects = from container in uPnpResponse.Elements(uPnpNamespaces.containers)
                           select new uParserObject { Type = (string)container.Element(uPnpNamespaces.uClass), Element = container };


            var uObjects2 = from container in uPnpResponse.Elements(uPnpNamespaces.items)
                            select new uParserObject { Type = (string)container.Element(uPnpNamespaces.uClass), Element = container };

            foreach (var uItem in uObjects.Concat(uObjects2))
            {
                uBaseObject uObject = CreateObjectFromXML(uItem);
                if (uObject != null)
                    list.Add(uObject);
            }

            return list;

        }

        internal static uBaseObject CreateObjectFromXML(uParserObject uItem)
        {
            
            uBaseObject uObject = null;
            uObject = uContainer.Create(uItem.Element);
            return uObject;
        }
    }

    internal class uParserObject
    {
        internal string Type
        { get; set; }

        internal XElement Element
        { get; set; }
    }
}
