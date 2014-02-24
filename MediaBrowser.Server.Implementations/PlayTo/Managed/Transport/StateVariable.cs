using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Transport
{
    public class StateVariable
    {
        public string Name
        { get; set; }

        public string DataType
        { get; set; }

        private List<string> _allowedValues = new List<string>();

        public List<string> AllowedValues
        {
            get
            {
                return _allowedValues;
            }
            set
            {
                _allowedValues = value;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        internal static StateVariable FromXml(XElement container)
        {
            List<string> allowedValues = new List<string>();
            var element = container.Descendants(uPnpNamespaces.svc + "allowedValueList").FirstOrDefault();
            if (element != null)
            {
                var values = element.Descendants(uPnpNamespaces.svc + "allowedValue");

                foreach (XElement child in values)
                    allowedValues.Add(child.Value);

            }         

            return new StateVariable
            {
                Name = (string)container.Element(uPnpNamespaces.svc + "name").Value,
                DataType = (string)container.Element(uPnpNamespaces.svc + "dataType").Value,
                AllowedValues = allowedValues
            };
        }
    }
}
