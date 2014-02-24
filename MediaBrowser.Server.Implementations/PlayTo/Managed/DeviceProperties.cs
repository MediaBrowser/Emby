using MediaBrowser.Server.Implementations.PlayTo.Managed.Entities;
using System.Collections.Generic;

namespace MediaBrowser.Server.Implementations.PlayTo.Managed
{
   public class DeviceProperties
    {
        private string _uuid = string.Empty;
        public string UUID
        {
            get
            {
                return _uuid;
            }
            set
            {
                _uuid = value;
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }

        private string _clientType = "DLNA";
        public string ClientType
        {
            get
            {
                return _clientType;
            }
            set
            {
                _clientType = value;
            }
        }

        private string _displayName = string.Empty;
        public string DisplayName
        {
            get
            {
                return string.IsNullOrEmpty(_displayName) ? _name : _displayName;
            }
            set
            {
                _displayName = value;
            }
        }

        private string _modelName = string.Empty;
        public string ModelName
        {
            get
            {
                return _modelName;
            }
            set
            {
                _modelName = value;
            }
        }

        private string _modelNumber = string.Empty;
        public string ModelNumber
        {
            get
            {
                return _modelNumber;
            }
            set
            {
                _modelNumber = value;
            }
        }

        private string _manufacturer = string.Empty;
        public string Manufacturer
        {
            get
            {
                return _manufacturer;
            }
            set
            {
                _manufacturer = value;
            }
        }

        private string _manufacturerUrl = string.Empty;
        public string ManufacturerUrl
        {
            get
            {
                return _manufacturerUrl;
            }
            set
            {
                _manufacturerUrl = value;
            }
        }

        private string _presentationUrl = string.Empty;
        public string PresentationUrl
        {
            get
            {
                return _presentationUrl;
            }
            set
            {
                _presentationUrl = value;
            }
        }

        private string _baseUrl = string.Empty;
        public string BaseUrl
        {
            get
            {
                return _baseUrl;
            }
            set
            {
                _baseUrl = value;
            }
        }

        private uIcon icon;
        public uIcon Icon
        {
            get
            {
                return icon;
            }
            set
            {
                icon = value;
            }
        }

        internal string _iconUrl;
        public string IconUrl
        {
            get
            {
                if (_iconUrl == null && icon != null)
                {
                    if (!icon.Url.StartsWith("/"))
                        _iconUrl = _baseUrl + "/" + icon.Url;
                    else
                        _iconUrl = _baseUrl + icon.Url;
                }
                return _iconUrl;
            }
        }

        private List<uService> _services = new List<uService>();
        public List<uService> Services
        {
            get
            {
                return _services;
            }
        }
    }
}
