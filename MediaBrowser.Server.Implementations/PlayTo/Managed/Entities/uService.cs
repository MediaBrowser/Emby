using System.Linq;
using System.Xml.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed.Entities
{
    public class uService
    {
        private string _serviceType;
        internal string ServiceType
        {
            get
            {
                return _serviceType;
            }
            set
            {
                _serviceType = value;
            }
        }

        private string _serviceId;
        internal string ServiceId
        {
            get
            {
                return _serviceId;
            }
            set
            {
                _serviceId = value;
            }
        }

        private string _scdpUrl;
        internal string SCPDURL
        {
            get
            {
                return _scdpUrl;
            }
            set
            {
                _scdpUrl = value;
            }
        }

        private string _controlURL;
        internal string ControlURL
        {
            get
            {
                return _controlURL;
            }
            set
            {
                _controlURL = value;
            }
        }

        private string _eventSubURL;
        internal string EventSubURL
        {
            get
            {
                return _eventSubURL;
            }
            set
            {
                _eventSubURL = value;
            }
        }

        internal uService(string serviceType, string serviceId, string scpdUrl, string controlUrl, string eventSubUrl)
        {
            _serviceType = serviceType;
            _serviceId = serviceId;
            _scdpUrl = scpdUrl;
            _controlURL = controlUrl;
            _eventSubURL = eventSubUrl;
        }

        internal static uService FromXml(XElement element)
        {
           string type = element.Descendants(uPnpNamespaces.ud.GetName("serviceType")).FirstOrDefault().Value;
           string id = element.Descendants(uPnpNamespaces.ud.GetName("serviceId")).FirstOrDefault().Value;
           string scpdUrl = element.Descendants(uPnpNamespaces.ud.GetName("SCPDURL")).FirstOrDefault().Value;
           string controlURL = element.Descendants(uPnpNamespaces.ud.GetName("controlURL")).FirstOrDefault().Value;
           string eventSubURL = element.Descendants(uPnpNamespaces.ud.GetName("eventSubURL")).FirstOrDefault().Value;

           return new uService(type, id, scpdUrl, controlURL, eventSubURL);
        }

        public override string ToString()
        {
            return string.Format("{0}", ServiceId);
        }
    }
}
