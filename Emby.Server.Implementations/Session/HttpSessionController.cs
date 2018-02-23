using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.System;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Server.Implementations.Session
{
    public class HttpSessionController : ISessionController
    {
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _json;
        private readonly ISessionManager _sessionManager;

        public SessionInfo Session { get; private set; }

        private readonly string _postUrl;

        public HttpSessionController(IHttpClient httpClient,
            IJsonSerializer json,
            SessionInfo session,
            string postUrl, ISessionManager sessionManager)
        {
            _httpClient = httpClient;
            _json = json;
            Session = session;
            _postUrl = postUrl;
            _sessionManager = sessionManager;
        }

        private string PostUrl
        {
            get
            {
                return string.Format("http://{0}{1}", Session.RemoteEndPoint, _postUrl);
            }
        }

        public bool IsSessionActive
        {
            get
            {
                return (DateTime.UtcNow - Session.LastActivityDate).TotalMinutes <= 10;
            }
        }

        public bool SupportsMediaControl
        {
            get { return true; }
        }

        private Task SendMessage(string name, CancellationToken cancellationToken)
        {
            return SendMessage(name, new Dictionary<string, string>(), cancellationToken);
        }

        private async Task SendMessage(string name,
            Dictionary<string, string> args,
            CancellationToken cancellationToken)
        {
            var url = PostUrl + "/" + name + ToQueryString(args);

            using ((await _httpClient.Post(new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken,
                BufferContent = false

            }).ConfigureAwait(false)))
            {

            }
        }

        private Task SendPlayCommand(PlayRequest command, CancellationToken cancellationToken)
        {
            var dict = new Dictionary<string, string>();

            dict["ItemIds"] = string.Join(",", command.ItemIds);

            if (command.StartPositionTicks.HasValue)
            {
                dict["StartPositionTicks"] = command.StartPositionTicks.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (command.AudioStreamIndex.HasValue)
            {
                dict["AudioStreamIndex"] = command.AudioStreamIndex.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (command.SubtitleStreamIndex.HasValue)
            {
                dict["SubtitleStreamIndex"] = command.SubtitleStreamIndex.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (command.StartIndex.HasValue)
            {
                dict["StartIndex"] = command.StartIndex.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(command.MediaSourceId))
            {
                dict["MediaSourceId"] = command.MediaSourceId;
            }

            return SendMessage(command.PlayCommand.ToString(), dict, cancellationToken);
        }

        private Task SendPlaystateCommand(PlaystateRequest command, CancellationToken cancellationToken)
        {
            var args = new Dictionary<string, string>();

            if (command.Command == PlaystateCommand.Seek)
            {
                if (!command.SeekPositionTicks.HasValue)
                {
                    throw new ArgumentException("SeekPositionTicks cannot be null");
                }

                args["SeekPositionTicks"] = command.SeekPositionTicks.Value.ToString(CultureInfo.InvariantCulture);
            }

            return SendMessage(command.Command.ToString(), args, cancellationToken);
        }

        private Task SendGeneralCommand(GeneralCommand command, CancellationToken cancellationToken)
        {
            return SendMessage(command.Name, command.Arguments, cancellationToken);
        }

        private string[] _supportedMessages = new string[] { "LibraryChanged", "ServerRestarting", "ServerShuttingDown", "RestartRequired" };
        public Task SendMessage<T>(string name, T data, CancellationToken cancellationToken)
        {
            if (!IsSessionActive)
            {
                return Task.FromResult(true);
            }

            if (string.Equals(name, "Play", StringComparison.OrdinalIgnoreCase))
            {
                return SendPlayCommand(data as PlayRequest, cancellationToken);
            }
            if (string.Equals(name, "PlayState", StringComparison.OrdinalIgnoreCase))
            {
                return SendPlaystateCommand(data as PlaystateRequest, cancellationToken);
            }
            if (string.Equals(name, "GeneralCommand", StringComparison.OrdinalIgnoreCase))
            {
                return SendGeneralCommand(data as GeneralCommand, cancellationToken);
            }

            if (!_supportedMessages.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return Task.FromResult(true);
            }

            var url = PostUrl + "/" + name;

            var options = new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken,
                BufferContent = false
            };

            if (data != null)
            {
                if (typeof(T) == typeof(string))
                {
                    var str = data as String;
                    if (!string.IsNullOrEmpty(str))
                    {
                        options.RequestContent = str;
                        options.RequestContentType = "application/json";
                    }
                }
                else
                {
                    options.RequestContent = _json.SerializeToString(data);
                    options.RequestContentType = "application/json";
                }
            }

            return _httpClient.Post(new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken,
                BufferContent = false
            });
        }

        private string ToQueryString(Dictionary<string, string> nvc)
        {
            var array = (from item in nvc
                         select string.Format("{0}={1}", WebUtility.UrlEncode(item.Key), WebUtility.UrlEncode(item.Value)))
                .ToArray();

            var args = string.Join("&", array);

            if (string.IsNullOrEmpty(args))
            {
                return args;
            }

            return "?" + args;
        }
    }
}
