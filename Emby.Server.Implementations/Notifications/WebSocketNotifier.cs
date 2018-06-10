using System;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Controller.Plugins;
using System.Linq;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Controller.Session;
using System.Collections.Generic;
using System.Threading;

namespace Emby.Server.Implementations.Notifications
{
    /// <summary>
    /// Notifies clients anytime a notification is added or udpated
    /// </summary>
    public class WebSocketNotifier : IServerEntryPoint
    {
        private readonly INotificationsRepository _notificationsRepo;

        private readonly ISessionManager _sessionManager;

        public WebSocketNotifier(INotificationsRepository notificationsRepo, ISessionManager sessionManager)
        {
            _notificationsRepo = notificationsRepo;
            _sessionManager = sessionManager;
        }

        public void Run()
        {
            _notificationsRepo.NotificationAdded += _notificationsRepo_NotificationAdded;
            _notificationsRepo.NotificationsMarkedRead += _notificationsRepo_NotificationsMarkedRead;
        }

        void _notificationsRepo_NotificationsMarkedRead(object sender, NotificationReadEventArgs e)
        {
            var list = e.IdList.ToList();

            list.Add(e.UserId);
            list.Add(e.IsRead.ToString().ToLower());

            var msg = string.Join("|", list.ToArray(list.Count));

            SendMessageToUserSession(new Guid(e.UserId), "NotificationsMarkedRead", msg);
        }

        void _notificationsRepo_NotificationAdded(object sender, NotificationUpdateEventArgs e)
        {
            var msg = e.Notification.UserId + "|" + e.Notification.Id;

            SendMessageToUserSession(new Guid(e.Notification.UserId), "NotificationAdded", msg);
        }

        private async void SendMessageToUserSession<T>(Guid userId, string name, T data)
        {
            try
            {
                await _sessionManager.SendMessageToUserSessions(new List<Guid> { userId }, name, data, CancellationToken.None);
            }
            catch (ObjectDisposedException)
            {

            }
            catch (Exception)
            {
                //Logger.ErrorException("Error sending message", ex);
            }
        }

        public void Dispose()
        {
            _notificationsRepo.NotificationAdded -= _notificationsRepo_NotificationAdded;
            _notificationsRepo.NotificationsMarkedRead -= _notificationsRepo_NotificationsMarkedRead;
        }
    }
}
