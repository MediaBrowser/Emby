
namespace MediaBrowser.Controller.Session
{
    public class AuthenticationRequest
    {
        private static readonly string _defaultDomain = @"local.emby.media";
        
        public string Username { get; set; }
        public string Domain { get; set; } 
        public string Password { get; set; }
        public string PasswordMd5 { get; set; }
        public string App { get; set; }
        public string AppVersion { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string RemoteEndPoint { get; set; }
        public bool EnforcePassword { get; set; } = true;

        public string DistinguishedName
        {
            get { return (string.IsNullOrWhiteSpace(Domain) ? _defaultDomain : Domain) + @"\" + Username; }
            set
            {
                var dnParts = value.Split('\\');
                if (dnParts.Length > 1)
                {
                    Username = dnParts[1];
                    Domain = dnParts[0];
                }
                else
                {
                    Username = value;
                    Domain = _defaultDomain;
                }
            }
        }

        public AuthenticationRequest()
        {
            Domain = _defaultDomain;
        }
    }
}
