
namespace Emby.Dlna.Configuration
{
    public class DlnaOptions
    {
        public bool EnablePlayTo { get; set; }
        public bool EnableServer { get; set; }
        public bool EnableDebugLog { get; set; }
        public bool BlastAliveMessages { get; set; }
        public int ClientDiscoveryIntervalSeconds { get; set; }
        public int AliveMessageIntervalSeconds { get; set; }
        public string DefaultUserId { get; set; }

        public DlnaOptions()
        {
            EnablePlayTo = true;
            EnableServer = true;
            BlastAliveMessages = true;
            ClientDiscoveryIntervalSeconds = 60;
            AliveMessageIntervalSeconds = 1800;
        }
    }
}
