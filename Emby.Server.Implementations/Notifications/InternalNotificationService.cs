using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Model.Notifications;
using System.Threading;
using System.Threading.Tasks;
using System;
using MediaBrowser.Model.Activity;

namespace Emby.Server.Implementations.Notifications
{
    public class InternalNotificationService : INotificationService, IConfigurableNotificationService
    {
        private readonly IActivityRepository _repo;

        public InternalNotificationService(IActivityRepository repo)
        {
            _repo = repo;
        }

        public string Name
        {
            get { return "Dashboard Notifications"; }
        }

        public Task SendNotification(UserNotification request, CancellationToken cancellationToken)
        {
            _repo.Create(new ActivityLogEntry
            {
                Name = request.Name,
                Type = "Custom",
                Overview = request.Description,
                ShortOverview = request.Description,
                Severity = GetSeverity(request.Level),
                UserId = request.User == null ? null : request.User.Id.ToString("N")
            });

            return Task.CompletedTask;
        }

        private MediaBrowser.Model.Logging.LogSeverity GetSeverity(NotificationLevel level)
        {
            switch (level)
            {
                case NotificationLevel.Error:
                    return MediaBrowser.Model.Logging.LogSeverity.Error;
                case NotificationLevel.Warning:
                    return MediaBrowser.Model.Logging.LogSeverity.Warn;
                default:
                    return MediaBrowser.Model.Logging.LogSeverity.Info;
            }
        }

        public bool IsEnabledForUser(User user)
        {
            return user.Policy.IsAdministrator;
        }

        public bool IsHidden
        {
            get { return true; }
        }

        public bool IsEnabled(string notificationType)
        {
            if (notificationType.IndexOf("playback", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return false;
            }
            if (notificationType.IndexOf("newlibrarycontent", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return false;
            }
            return true;
        }
    }
}
