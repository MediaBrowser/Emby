using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Connect;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Users;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Entities
{
    /// <summary>
    /// Class User
    /// </summary>
    public class User : BaseItem
    {
        public static IUserManager UserManager { get; set; }
        public static IXmlSerializer XmlSerializer { get; set; }

        public string DN { get; set; }
        public string DomainUid { get; set; }

        public string EasyPassword { get; set; }

        public string ConnectUserName { get; set; }
        public string ConnectUserId { get; set; }
        public UserLinkType? ConnectLinkType { get; set; }
        public string ConnectAccessKey { get; set; }

         private string _name;
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public override string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;

                // lazy load this again
                SortName = null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is owned item.
        /// </summary>
        /// <value><c>true</c> if this instance is owned item; otherwise, <c>false</c>.</value>
        [IgnoreDataMember]
        public override bool IsOwnedItem
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the root folder.
        /// </summary>
        /// <value>The root folder.</value>
        [IgnoreDataMember]
        public Folder RootFolder
        {
            get
            {
                return LibraryManager.GetUserRootFolder();
            }
        }

        /// <summary>
        /// Gets or sets the last login date.
        /// </summary>
        /// <value>The last login date.</value>
        public DateTime? LastLoginDate { get; set; }
        /// <summary>
        /// Gets or sets the last activity date.
        /// </summary>
        /// <value>The last activity date.</value>
        public DateTime? LastActivityDate { get; set; }

        private volatile UserConfiguration _config;
        private readonly object _configSyncLock = new object();
        [IgnoreDataMember]
        public UserConfiguration Configuration
        {
            get
            {
                if (_config == null)
                {
                    lock (_configSyncLock)
                    {
                        if (_config == null)
                        {
                            _config = UserManager.GetUserConfiguration(this);
                        }
                    }
                }

                return _config;
            }
            set { _config = value; }
        }

        private volatile UserPolicy _policy;
        private readonly object _policySyncLock = new object();
        [IgnoreDataMember]
        public UserPolicy Policy
        {
            get
            {
                if (_policy == null)
                {
                    lock (_policySyncLock)
                    {
                        if (_policy == null)
                        {
                            _policy = UserManager.GetUserPolicy(this);
                        }
                    }
                }
                
                return _policy;
            }
            set { _policy = value; }
        }

        /// <summary>
        /// Renames the user.
        /// </summary>
        /// <param name="newName">The new name.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public Task Rename(string newName)
        {
            if (string.IsNullOrEmpty(newName))
            {
                throw new ArgumentNullException("newName");
            }

            Name = newName;

			return RefreshMetadata(new MetadataRefreshOptions(new DirectoryService(FileSystem))
            {
                ReplaceAllMetadata = true,
                ImageRefreshMode = ImageRefreshMode.FullRefresh,
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                ForceSave = true

            }, CancellationToken.None);
        }

        public override Task UpdateToRepository(ItemUpdateType updateReason, CancellationToken cancellationToken)
        {
            return UserManager.UpdateUser(this);
        }

         public bool IsParentalScheduleAllowed()
        {
            return IsParentalScheduleAllowed(DateTime.UtcNow);
        }

        public bool IsParentalScheduleAllowed(DateTime date)
        {
            var schedules = Policy.AccessSchedules;

            if (schedules.Length == 0)
            {
                return true;
            }

            return schedules.Any(i => IsParentalScheduleAllowed(i, date));
        }

        private bool IsParentalScheduleAllowed(AccessSchedule schedule, DateTime date)
        {
            if (date.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Utc date expected");
            }

            var localTime = date.ToLocalTime();

            return DayOfWeekHelper.GetDaysOfWeek(schedule.DayOfWeek).Contains(localTime.DayOfWeek) &&
                IsWithinTime(schedule, localTime);
        }

        private bool IsWithinTime(AccessSchedule schedule, DateTime localTime)
        {
            var hour = localTime.TimeOfDay.TotalHours;

            return hour >= schedule.StartHour && hour <= schedule.EndHour;
        }

        public bool IsFolderGrouped(Guid id)
        {
            return Configuration.GroupedFolders.Select(i => new Guid(i)).Contains(id);
        }

        [IgnoreDataMember]
        public override bool SupportsPeople
        {
            get
            {
                return false;
            }
        }

        public string FQDN { get; set; }
    }
}
