using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Serialization;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using MediaBrowser.Common;

namespace Emby.Server.Implementations.Session
{
    public class FirebaseSessionController : ISessionController
    {
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _json;
        private readonly ISessionManager _sessionManager;

        public SessionInfo Session { get; private set; }

        private readonly string _token;

        private IApplicationHost _appHost;
        private string _senderId;
        private string _applicationId;

        public FirebaseSessionController(IHttpClient httpClient,
            IApplicationHost appHost,
            IJsonSerializer json,
            SessionInfo session,
            string token, ISessionManager sessionManager)
        {
            _httpClient = httpClient;
            _json = json;
            _appHost = appHost;
            Session = session;
            _token = token;
            _sessionManager = sessionManager;

            _applicationId = _appHost.GetValue("firebase_applicationid");
            _senderId = _appHost.GetValue("firebase_senderid");
        }

        public static bool IsSupported(IApplicationHost appHost)
        {
            return !string.IsNullOrEmpty(appHost.GetValue("firebase_applicationid")) && !string.IsNullOrEmpty(appHost.GetValue("firebase_senderid"));
        }

        public bool IsSessionActive
        {
            get
            {
                return (DateTime.UtcNow - Session.LastActivityDate).TotalHours <= 1;
            }
        }

        public bool SupportsMediaControl
        {
            get { return true; }
        }

        public async Task SendMessage<T>(string name, T data, CancellationToken cancellationToken)
        {
            if (!IsSessionActive)
            {
                return;
            }

            if (string.IsNullOrEmpty(_senderId) || string.IsNullOrEmpty(_applicationId))
            {
                return;
            }

            string strData = _json.SerializeToString(data);
            var req = new FirebaseBody
            {
                to = _token,
                data = new FirebaseData
                {
                    msgdata = strData
                }
            };

            var byteArray = Encoding.UTF8.GetBytes(_json.SerializeToString(req));

            var enableLogging = false;

#if DEBUG
            enableLogging = true;
#endif

            var options = new HttpRequestOptions
            {
                Url = "https://fcm.googleapis.com/fcm/send",
                RequestContentType = "application/json",
                RequestContentBytes = byteArray,
                CancellationToken = cancellationToken,
                LogRequest = enableLogging,
                LogResponse = enableLogging,
                LogErrors = enableLogging
            };

            options.RequestHeaders["Authorization"] = string.Format("key={0}", _applicationId);
            options.RequestHeaders["Sender"] = string.Format("id={0}", _senderId);

            using (var response = await _httpClient.Post(options).ConfigureAwait(false))
            {

            }
        }
    }

    internal class FirebaseBody
    {
        public string to { get; set; }
        public FirebaseData data { get; set; }
    }
    internal class FirebaseData
    {
        public string msgdata { get; set; }
    }
}
