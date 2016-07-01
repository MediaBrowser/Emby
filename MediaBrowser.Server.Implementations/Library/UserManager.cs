using MediaBrowser.Common.Security;
using MediaBrowser.Common.Events;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Connect;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Connect;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using MediaBrowser.Providers.Authentication;

namespace MediaBrowser.Server.Implementations.Library
{
    /// <summary>
    /// Class UserManager
    /// </summary>
    public class UserManager : IUserManager
    {
        /// <summary>
        /// Gets the users.
        /// </summary>
        /// <value>The users.</value>
        public IEnumerable<User> Users { get; private set; }

        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Gets or sets the configuration manager.
        /// </summary>
        /// <value>The configuration manager.</value>
        private IServerConfigurationManager ConfigurationManager { get; set; }

        /// <summary>
        /// Gets the active user repository
        /// </summary>
        /// <value>The user repository.</value>
        private IUserRepository UserRepository { get; set; }
        public event EventHandler<GenericEventArgs<User>> UserPasswordChanged;

        private readonly IXmlSerializer _xmlSerializer;
        private readonly IJsonSerializer _jsonSerializer;

        private readonly INetworkManager _networkManager;

        private readonly Func<IImageProcessor> _imageProcessorFactory;
        private readonly Func<IDtoService> _dtoServiceFactory;
        private readonly Func<IConnectManager> _connectFactory;
        private readonly IServerApplicationHost _appHost;
        private readonly IFileSystem _fileSystem;
        private string DefaultDomain;
        private Dictionary<string, IDirectoriesProvider> DomainsProviders { get; set; }
        private List<IDirectoriesProvider> DirectoriesProviders { get; set; }

        public UserManager(ILogger logger, IServerConfigurationManager configurationManager, IUserRepository userRepository, IXmlSerializer xmlSerializer, INetworkManager networkManager, Func<IImageProcessor> imageProcessorFactory, Func<IDtoService> dtoServiceFactory, Func<IConnectManager> connectFactory, IServerApplicationHost appHost, IJsonSerializer jsonSerializer, IFileSystem fileSystem)
        {
            _logger = logger;
            UserRepository = userRepository;
            _xmlSerializer = xmlSerializer;
            _networkManager = networkManager;
            _imageProcessorFactory = imageProcessorFactory;
            _dtoServiceFactory = dtoServiceFactory;
            _connectFactory = connectFactory;
            _appHost = appHost;
            _jsonSerializer = jsonSerializer;
            _fileSystem = fileSystem;
            ConfigurationManager = configurationManager;
            Users = new List<User>();
            DefaultDomain = "Local";
            DeletePinFile();
        }

        #region UserUpdated Event
        /// <summary>
        /// Occurs when [user updated].
        /// </summary>
        public event EventHandler<GenericEventArgs<User>> UserUpdated;
        public event EventHandler<GenericEventArgs<User>> UserConfigurationUpdated;
        public event EventHandler<GenericEventArgs<User>> UserLockedOut;

        /// <summary>
        /// Called when [user updated].
        /// </summary>
        /// <param name="user">The user.</param>
        private void OnUserUpdated(User user)
        {
            EventHelper.FireEventIfNotNull(UserUpdated, this, new GenericEventArgs<User> { Argument = user }, _logger);
        }
        #endregion

        #region UserDeleted Event
        /// <summary>
        /// Occurs when [user deleted].
        /// </summary>
        public event EventHandler<GenericEventArgs<User>> UserDeleted;
        /// <summary>
        /// Called when [user deleted].
        /// </summary>
        /// <param name="user">The user.</param>
        private void OnUserDeleted(User user)
        {
            EventHelper.QueueEventIfNotNull(UserDeleted, this, new GenericEventArgs<User> { Argument = user }, _logger);
        }
        #endregion

        /// <summary>
        /// Gets a User by Id
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>User.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public User GetUserById(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            return Users.FirstOrDefault(u => u.Id == id);
        }

        /// <summary>
        /// Gets the user by identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>User.</returns>
        public User GetUserById(string id)
        {
            return GetUserById(new Guid(id));
        }

        public User GetUserByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("name");
            }

            return Users.FirstOrDefault(i => string.Equals(name, i.DN, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(DefaultDomain + "/" + name, i.DN, StringComparison.OrdinalIgnoreCase));
            
        }

        public async Task Initialize()
        {
            Users = await LoadUsers().ConfigureAwait(false);
        }

        public Task<bool> AuthenticateUser(string distinguishedName, string password, string remoteEndPoint)
        {
            return AuthenticateUser(distinguishedName, password, null, remoteEndPoint);
        }

        public async Task<bool> AuthenticateUser(string dn, string password, string passwordMd5, string remoteEndPoint)
        {

            var user = GetUserByName(dn);

            if (user == null)
            {
                throw new SecurityException("Invalid username or password entered.");
            }

            if (user.Policy.IsDisabled)
            {
                throw new SecurityException(string.Format("The {0} account is currently disabled. Please consult with your administrator.", user.Name));
            }

            var success = false;

            // Authenticate using local credentials if not a guest
            if (!user.ConnectLinkType.HasValue || user.ConnectLinkType.Value != UserLinkType.Guest)
            {
                success = await DomainsProviders[user.FQDN].Authenticate(user.DomainUid, user.FQDN, password);

                if (!success && _networkManager.IsInLocalNetwork(remoteEndPoint) && user.Configuration.EnableLocalPassword)
                {
                    success = string.Equals(GetLocalPasswordHash(user), password.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase);
                }
            }

            // Update LastActivityDate and LastLoginDate, then save
            if (success)
            {
                user.LastActivityDate = user.LastLoginDate = DateTime.UtcNow;
                UpdateInvalidLoginAttemptCount(user, 0);
            }
            else
            {
                UpdateInvalidLoginAttemptCount(user, user.Policy.InvalidLoginAttemptCount + 1);
            }
            await UpdateUser(user).ConfigureAwait(false);
            _logger.Info("Authentication request for {0} {1}.", user.Name, success ? "has succeeded" : "has been denied");

            return success;
        }

        private void UpdateInvalidLoginAttemptCount(User user, int newValue)
        {
            if (user.Policy.InvalidLoginAttemptCount != newValue || newValue > 0)
            {
                user.Policy.InvalidLoginAttemptCount = newValue;

                var maxCount = user.Policy.IsAdministrator ? 3 : 5;

                var fireLockout = false;

                if (newValue >= maxCount)
                {
                    //_logger.Debug("Disabling user {0} due to {1} unsuccessful login attempts.", user.Name, newValue.ToString(CultureInfo.InvariantCulture));
                    //user.Policy.IsDisabled = true;

                    //fireLockout = true;
                }

                if (fireLockout)
                {
                    if (UserLockedOut != null)
                    {
                        EventHelper.FireEventIfNotNull(UserLockedOut, this, new GenericEventArgs<User>(user), _logger);
                    }
                }
            }
        }

        private string GetLocalPasswordHash(User user)
        {
            return string.IsNullOrEmpty(user.EasyPassword)
                ? Crypto.GetSha1(string.Empty)
                : user.EasyPassword;
        }

        private void GetDomains()
        {
            DomainsProviders = new Dictionary<string, IDirectoriesProvider>();
            DirectoriesProviders.ToList().ForEach(p =>
                 p.GetDomains().ToList().ForEach(d => DomainsProviders[d] = p)
            );
        }

        /// <summary>
        /// Loads the users from the repository
        /// </summary>
        /// <returns>IEnumerable{User}.</returns>
        private async Task<IEnumerable<User>> LoadUsers()
        {
            GetDomains();
            var users = UserRepository.RetrieveAllUsers().ToList();

            DomainsProviders.ToList().ForEach(d => {
                d.Value.RetrieveAll(d.Key).Result.ToList().ForEach(e => {
                    if(!users.Any(u => u.DN == e.DN))
                    {
                        users.Add(UserRepository.CreateUser(e).Result);
                    }
                });
            });

            //At least one local user has to be admin if not make all local users admins
            if (users.Where(u => (u.FQDN == "Local" && u.Policy.IsAdministrator == true)).Count() == 0)
            {
                foreach (var u in users.Where(u => (u.FQDN == "Local"))){
                    u.Policy.IsAdministrator = true;
                    u.Policy.EnableContentDeletion = true;
                    u.Policy.EnableRemoteControlOfOtherUsers = true;
                    UserRepository.UpdateUserPolicy(u).Wait();
                }
            }
            return users;
        }

        public UserDto GetUserDto(User user, string remoteEndPoint = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            var hasConfiguredPassword = !DomainsProviders[user.FQDN].Authenticate(user.DomainUid, user.FQDN, String.Empty).Result;
            var hasConfiguredEasyPassword = !(GetLocalPasswordHash(user) == Crypto.GetSha1(String.Empty));

            var hasPassword = user.Configuration.EnableLocalPassword && !string.IsNullOrEmpty(remoteEndPoint) && _networkManager.IsInLocalNetwork(remoteEndPoint) ?
                hasConfiguredEasyPassword :
                hasConfiguredPassword;

            var dto = new UserDto
            {
                Id = user.Id.ToString("N"),
                DN = user.DN,
                FQDN = user.FQDN,
                Name = user.Name,
                HasPassword = hasPassword,
                HasConfiguredPassword = hasConfiguredPassword,
                HasConfiguredEasyPassword = hasConfiguredEasyPassword,
                LastActivityDate = user.LastActivityDate,
                LastLoginDate = user.LastLoginDate,
                Configuration = user.Configuration,
                ConnectLinkType = user.ConnectLinkType,
                ConnectUserId = user.ConnectUserId,
                ConnectUserName = user.ConnectUserName,
                ServerId = _appHost.SystemId,
                Policy = user.Policy
            };

            var image = user.GetImageInfo(ImageType.Primary, 0);

            if (image != null)
            {
                dto.PrimaryImageTag = GetImageCacheTag(user, image);

                try
                {
                    _dtoServiceFactory().AttachPrimaryImageAspectRatio(dto, user);
                }
                catch (Exception ex)
                {
                    // Have to use a catch-all unfortunately because some .net image methods throw plain Exceptions
                    _logger.ErrorException("Error generating PrimaryImageAspectRatio for {0}", ex, user.Name);
                }
            }

            return dto;
        }

        public UserDto GetOfflineUserDto(User user)
        {
            var dto = GetUserDto(user);

            var offlinePasswordHash = GetLocalPasswordHash(user);
            dto.HasPassword = !(GetLocalPasswordHash(user) == Crypto.GetSha1(String.Empty));

            dto.OfflinePasswordSalt = Guid.NewGuid().ToString("N");

            // Hash the pin with the device Id to create a unique result for this device
            dto.OfflinePassword = Crypto.GetSha1((offlinePasswordHash + dto.OfflinePasswordSalt).ToLower());

            dto.ServerName = _appHost.FriendlyName;

            return dto;
        }

        private string GetImageCacheTag(BaseItem item, ItemImageInfo image)
        {
            try
            {
                return _imageProcessorFactory().GetImageCacheTag(item, image);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting {0} image info for {1}", ex, image.Type, image.Path);
                return null;
            }
        }

        /// <summary>
        /// Refreshes metadata for each user
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task RefreshUsersMetadata(CancellationToken cancellationToken)
        {
            var tasks = Users.Select(user => user.RefreshMetadata(new MetadataRefreshOptions(_fileSystem), cancellationToken)).ToList();

            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Renames the user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        /// <exception cref="System.ArgumentException"></exception>
        public async Task RenameUser(User user, string newName)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            if (string.IsNullOrEmpty(newName))
            {
                throw new ArgumentNullException("newName");
            }

            if (Users.Any(u => u.Id != user.Id && u.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(string.Format("A user with the name '{0}' already exists.", newName));
            }

            if (user.Name.Equals(newName, StringComparison.Ordinal))
            {
                throw new ArgumentException("The new and old names must be different.");
            }

            var e = new DirectoryEntry(user.Name, user.FQDN);

            await DomainsProviders[user.FQDN].UpdateEntry(user.DomainUid, e);

            await user.Rename(newName);

            OnUserUpdated(user);
        }

        /// <summary>
        /// Updates the user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <exception cref="System.ArgumentNullException">user</exception>
        /// <exception cref="System.ArgumentException"></exception>
        public async Task UpdateUser(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            if (user.Id == Guid.Empty || !Users.Any(u => u.Id.Equals(user.Id)))
            {
                throw new ArgumentException(string.Format("User with name '{0}' and Id {1} does not exist.", user.Name, user.Id));
            }

            await UserRepository.UpdateEntry(user, CancellationToken.None).ConfigureAwait(false);

            OnUserUpdated(user);
        }

        public event EventHandler<GenericEventArgs<User>> UserCreated;

        private readonly SemaphoreSlim _userListLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Creates the user.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>User.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        /// <exception cref="System.ArgumentException"></exception>
        public async Task<User> CreateUser(string name, string fqdn="Local")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("name");
            }

            if (Users.Any(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && u.FQDN.Equals(fqdn, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(string.Format("A user with the name '{0}'  already exists on '{1}'.", name,fqdn));
            }

            await _userListLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                if (!Users.Any(u => u.Name == name && u.FQDN == fqdn))
                {
                    var entry = await DomainsProviders[fqdn].CreateEntry(name, fqdn);
                    var user = await UserRepository.CreateUser(entry).ConfigureAwait(false);   

                    var users = Users.ToList();
                    users.Add(user);
                    Users = users;

                    EventHelper.QueueEventIfNotNull(UserCreated, this, new GenericEventArgs<User> { Argument = user }, _logger);

                    return user;
              }
            }
            finally
            {
                _userListLock.Release();
            }
            return null;
        }

        /// <summary>
        /// Deletes the user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        /// <exception cref="System.ArgumentException"></exception>
        public async Task DeleteUser(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            if (user.ConnectLinkType.HasValue)
            {
                await _connectFactory().RemoveConnect(user.Id.ToString("N")).ConfigureAwait(false);
            }

            var allUsers = Users.ToList();

            if (allUsers.FirstOrDefault(u => u.Id == user.Id) == null)
            {
                throw new ArgumentException(string.Format("The user cannot be deleted because there is no user with the Name {0} and Id {1}.", user.Name, user.Id));
            }

            if (allUsers.Count == 1)
            {
                throw new ArgumentException(string.Format("The user '{0}' cannot be deleted because there must be at least one user in the system.", user.Name));
            }

            if (user.Policy.IsAdministrator && allUsers.Count(i => i.Policy.IsAdministrator) == 1)
            {
                throw new ArgumentException(string.Format("The user '{0}' cannot be deleted because there must be at least one admin user in the system.", user.Name));
            }

            await _userListLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                await DomainsProviders[user.FQDN].DeleteEntry(user.Name, user.FQDN, CancellationToken.None);
                await UserRepository.DeleteUser(user, CancellationToken.None).ConfigureAwait(false);

                // Force this to be lazy loaded again
                Users = await LoadUsers().ConfigureAwait(false);

                OnUserDeleted(user);
            }
            finally
            {
                _userListLock.Release();
            }
        }

        /// <summary>
        /// Resets the password by clearing it.
        /// </summary>
        /// <returns>Task.</returns>
        public Task ResetPassword(User user)
        {
            return ChangePassword(user, Crypto.GetSha1(string.Empty));
        }

        public Task ResetEasyPassword(User user)
        {
            return ChangeEasyPassword(user, Crypto.GetSha1(string.Empty));
        }

        public async Task ChangePassword(User user, string newPassword)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            if (user.ConnectLinkType.HasValue && user.ConnectLinkType.Value == UserLinkType.Guest)
            {
                throw new ArgumentException("Passwords for guests cannot be changed.");
            }

            
            await DomainsProviders[user.FQDN].UpdatePassword(user.DomainUid,user.FQDN,newPassword).ConfigureAwait(false);

            EventHelper.FireEventIfNotNull(UserPasswordChanged, this, new GenericEventArgs<User>(user), _logger);
        }

        public async Task ChangeEasyPassword(User user, string newPasswordSha1)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (string.IsNullOrWhiteSpace(newPasswordSha1))
            {
                throw new ArgumentNullException("newPasswordSha1");
            }

            user.EasyPassword = newPasswordSha1;

            await UpdateUser(user).ConfigureAwait(false);

            EventHelper.FireEventIfNotNull(UserPasswordChanged, this, new GenericEventArgs<User>(user), _logger);
        }



        private string PasswordResetFile
        {
            get { return Path.Combine(ConfigurationManager.ApplicationPaths.ProgramDataPath, "passwordreset.txt"); }
        }

        private string _lastPin;
        private PasswordPinCreationResult _lastPasswordPinCreationResult;
        private int _pinAttempts;

        private PasswordPinCreationResult CreatePasswordResetPin()
        {
            var num = new Random().Next(1, 9999);

            var path = PasswordResetFile;

            var pin = num.ToString("0000", CultureInfo.InvariantCulture);
            _lastPin = pin;

            var time = TimeSpan.FromMinutes(5);
            var expiration = DateTime.UtcNow.Add(time);

            var text = new StringBuilder();

            var localAddress = _appHost.GetLocalApiUrl().Result ?? string.Empty;

            text.AppendLine("Use your web browser to visit:");
            text.AppendLine(string.Empty);
            text.AppendLine(localAddress + "/web/forgotpasswordpin.html");
            text.AppendLine(string.Empty);
            text.AppendLine("Enter the following pin code:");
            text.AppendLine(string.Empty);
            text.AppendLine(pin);
            text.AppendLine(string.Empty);
            text.AppendLine("The pin code will expire at " + expiration.ToLocalTime().ToShortDateString() + " " + expiration.ToLocalTime().ToShortTimeString());

            _fileSystem.WriteAllText(path, text.ToString(), Encoding.UTF8);

            var result = new PasswordPinCreationResult
            {
                PinFile = path,
                ExpirationDate = expiration
            };

            _lastPasswordPinCreationResult = result;
            _pinAttempts = 0;

            return result;
        }

        public ForgotPasswordResult StartForgotPasswordProcess(string enteredUsername, bool isInNetwork)
        {
            DeletePinFile();

            var user = string.IsNullOrWhiteSpace(enteredUsername) ?
                null :
                GetUserByName(enteredUsername);

            if (user != null && user.ConnectLinkType.HasValue && user.ConnectLinkType.Value == UserLinkType.Guest)
            {
                throw new ArgumentException("Unable to process forgot password request for guests.");
            }

            var action = ForgotPasswordAction.InNetworkRequired;
            string pinFile = null;
            DateTime? expirationDate = null;

            if (user != null && !user.Policy.IsAdministrator)
            {
                action = ForgotPasswordAction.ContactAdmin;
            }
            else
            {
                if (isInNetwork)
                {
                    action = ForgotPasswordAction.PinCode;
                }

                var result = CreatePasswordResetPin();
                pinFile = result.PinFile;
                expirationDate = result.ExpirationDate;
            }

            return new ForgotPasswordResult
            {
                Action = action,
                PinFile = pinFile,
                PinExpirationDate = expirationDate
            };
        }

        public async Task<PinRedeemResult> RedeemPasswordResetPin(string pin)
        {
            DeletePinFile();

            var usersReset = new List<string>();

            var valid = !string.IsNullOrWhiteSpace(_lastPin) &&
                string.Equals(_lastPin, pin, StringComparison.OrdinalIgnoreCase) &&
                _lastPasswordPinCreationResult != null &&
                _lastPasswordPinCreationResult.ExpirationDate > DateTime.UtcNow;

            if (valid)
            {
                _lastPin = null;
                _lastPasswordPinCreationResult = null;

                var users = Users.Where(i => !i.ConnectLinkType.HasValue || i.ConnectLinkType.Value != UserLinkType.Guest)
                        .ToList();

                foreach (var user in users)
                {
                    await ResetPassword(user).ConfigureAwait(false);

                    if (user.Policy.IsDisabled)
                    {
                        user.Policy.IsDisabled = false;
                        await UpdateUserPolicy(user, user.Policy, true).ConfigureAwait(false);
                    }
                    usersReset.Add(user.Name);
                }
            }
            else
            {
                _pinAttempts++;
                if (_pinAttempts >= 3)
                {
                    _lastPin = null;
                    _lastPasswordPinCreationResult = null;
                }
            }

            return new PinRedeemResult
            {
                Success = valid,
                UsersReset = usersReset.ToArray()
            };
        }

        private void DeletePinFile()
        {
            try
            {
                _fileSystem.DeleteFile(PasswordResetFile);
            }
            catch
            {

            }
        }

        class PasswordPinCreationResult
        {
            public string PinFile { get; set; }
            public DateTime ExpirationDate { get; set; }
        }

        public UserPolicy GetUserPolicy(User user)
        {
            try
            {
                return UserRepository.RetrieveUser(user.Id, CancellationToken.None).Result.Policy;
             }
            catch { return new UserPolicy(); }
        }

        private UserPolicy GetDefaultPolicy(User user)
        {
            return new UserPolicy
            {
                EnableSync = true
            };
        }

 
        public Task UpdateUserPolicy(string userId, UserPolicy userPolicy)
        {
            _logger.Info("Updating policy for userId " + userId);
            var user = GetUserById(userId);
            return UpdateUserPolicy(user, userPolicy, true);
        }

        private async Task UpdateUserPolicy(User user, UserPolicy userPolicy, bool fireEvent)
        {
            user.Policy = userPolicy;
            await UserRepository.UpdateUserPolicy(user);

            await UpdateConfiguration(user, user.Configuration, true).ConfigureAwait(false);
        }



 
        public UserConfiguration GetUserConfiguration(User user)
        {
            try
            {
                return UserRepository.RetrieveUser(user.Id, CancellationToken.None).Result.Configuration;
                    } catch { return new UserConfiguration(); }
        }

        public Task UpdateConfiguration(string userId, UserConfiguration config)
        {
            var user = GetUserById(userId);
            return UpdateConfiguration(user, config, true);
        }

        private async Task UpdateConfiguration(User user, UserConfiguration config, bool fireEvent)
        {
            user.Configuration = config;
            await UserRepository.UpdateUserConfig(user);

            if (fireEvent)
            {
                EventHelper.FireEventIfNotNull(UserConfigurationUpdated, this, new GenericEventArgs<User> { Argument = user }, _logger);
            }
        }

        public void AddParts(IEnumerable<IDirectoriesProvider> providers)
        {
            _logger.Info("----------ADDING PARTS------------");
            DirectoriesProviders = providers.ToList();
            Initialize().Wait();
            _logger.Info("----------INITIAL------------");
        }

        public Task<User> CreateUser(string name)
        {
            return CreateUser(name, "Local");
        }
    }
}