using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Server.Implementations.Session
{
    public class WebSocketController : ISessionController, IDisposable
    {
        public SessionInfo Session { get; private set; }
        public IReadOnlyList<IWebSocketConnection> Sockets { get; private set; }

        private readonly ILogger _logger;

        private readonly ISessionManager _sessionManager;

        public WebSocketController(SessionInfo session, ILogger logger, ISessionManager sessionManager)
        {
            Session = session;
            _logger = logger;
            _sessionManager = sessionManager;
            Sockets = new List<IWebSocketConnection>();
        }

        private bool HasOpenSockets
        {
            get { return GetActiveSockets().Any(); }
        }

        public bool SupportsMediaControl
        {
            get { return HasOpenSockets; }
        }

        private bool _isActive;
        private DateTime _lastActivityDate;
        public bool IsSessionActive
        {
            get
            {
                if (HasOpenSockets)
                {
                    return true;
                }

                //return false;
                return _isActive && (DateTime.UtcNow - _lastActivityDate).TotalMinutes <= 10;
            }
        }

        public void OnActivity()
        {
            _isActive = true;
            _lastActivityDate = DateTime.UtcNow;
        }

        private IEnumerable<IWebSocketConnection> GetActiveSockets()
        {
            return Sockets
                .OrderByDescending(i => i.LastActivityDate)
                .Where(i => i.State == WebSocketState.Open);
        }

        public void AddWebSocket(IWebSocketConnection connection)
        {
            var sockets = Sockets.ToList();
            sockets.Add(connection);

            Sockets = sockets;

            connection.Closed += connection_Closed;
        }

        void connection_Closed(object sender, EventArgs e)
        {
            if (!GetActiveSockets().Any())
            {
                _isActive = false;

                try
                {
                    _sessionManager.ReportSessionEnded(Session.Id);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error reporting session ended.", ex);
                }
            }
        }

        private IWebSocketConnection GetActiveSocket()
        {
            var socket = GetActiveSockets()
                .FirstOrDefault();

            if (socket == null)
            {
                throw new InvalidOperationException("The requested session does not have an open web socket.");
            }

            return socket;
        }

        public Task SendPlayCommand(PlayRequest command, CancellationToken cancellationToken)
        {
            return SendMessageInternal(new WebSocketMessage<PlayRequest>
            {
                MessageType = "Play",
                Data = command

            }, cancellationToken);
        }

        public Task SendPlaystateCommand(PlaystateRequest command, CancellationToken cancellationToken)
        {
            return SendMessageInternal(new WebSocketMessage<PlaystateRequest>
            {
                MessageType = "Playstate",
                Data = command

            }, cancellationToken);
        }

        public Task SendGeneralCommand(GeneralCommand command, CancellationToken cancellationToken)
        {
            return SendMessageInternal(new WebSocketMessage<GeneralCommand>
            {
                MessageType = "GeneralCommand",
                Data = command

            }, cancellationToken);
        }

        public Task SendPlaybackStartNotification(SessionInfoDto sessionInfo, CancellationToken cancellationToken)
        {
            return SendMessagesInternal(new WebSocketMessage<SessionInfoDto>
            {
                MessageType = "PlaybackStart",
                Data = sessionInfo

            }, cancellationToken);
        }

        public Task SendPlaybackStoppedNotification(SessionInfoDto sessionInfo, CancellationToken cancellationToken)
        {
            return SendMessagesInternal(new WebSocketMessage<SessionInfoDto>
            {
                MessageType = "PlaybackStopped",
                Data = sessionInfo

            }, cancellationToken);
        }

        public Task SendMessage<T>(string name, T data, CancellationToken cancellationToken)
        {
            return SendMessagesInternal(new WebSocketMessage<T>
            {
                Data = data,
                MessageType = name

            }, cancellationToken);
        }

        private Task SendMessageInternal<T>(WebSocketMessage<T> message, CancellationToken cancellationToken)
        {
            var socket = GetActiveSocket();

            return socket.SendAsync(message, cancellationToken);
        }

        private Task SendMessagesInternal<T>(WebSocketMessage<T> message, CancellationToken cancellationToken)
        {
            var tasks = GetActiveSockets().Select(i => Task.Run(async () =>
            {
                try
                {
                    await i.SendAsync(message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error sending web socket message", ex);
                }

            }, cancellationToken));

            return Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            foreach (var socket in Sockets.ToList())
            {
                socket.Closed -= connection_Closed;
            }
            GC.SuppressFinalize(this);
        }
    }
}
