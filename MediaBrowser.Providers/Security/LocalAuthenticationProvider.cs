using MediaBrowser.Controller.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Net;
using MediaBrowser.Common.Security;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Server.Implementations.Library;
using MediaBrowser.Model.Connect;

namespace MediaBrowser.Providers.Security
{
    public class LocalAuthenticationProvider : IAuthenticationProvider
    {
        private readonly UserManager _userManager;
        private readonly ILogger _logger;
        private readonly INetworkManager _networkManager;

        public LocalAuthenticationProvider(IUserManager userManager, ILogger logger, INetworkManager networkManager)
        {
            _userManager = userManager as UserManager;
            _logger = logger;
            _networkManager = networkManager;
        }
        public async Task<bool> Authenticate(AuthenticationRequest authRequest)
        {
            if (authRequest.Domain != @"local.emby.media") { return false; }

            if (string.IsNullOrWhiteSpace(authRequest.Username))
            {
                throw new ArgumentNullException("username");
            }            

            var user = _userManager.GetUserByName(authRequest.Username);

            if (user == null)
            {
                throw new SecurityException("Invalid username or password entered.");
            }

            if (user.Policy.IsDisabled)
            {
                throw new SecurityException(string.Format("The {0} account is currently disabled. Please consult with your administrator.", user.Name));
            }

            var success = !authRequest.EnforcePassword;

            // Authenticate using local credentials if not a guest
            if ((!user.ConnectLinkType.HasValue || user.ConnectLinkType.Value != UserLinkType.Guest) &&  !success)
            {
                success = string.Equals(user.Password ?? string.Empty.GetSha1Hash(), authRequest.Password.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase);

                if (!success && _networkManager.IsInLocalNetwork(authRequest.RemoteEndPoint) && user.Configuration.EnableLocalPassword)
                {
                    success = string.Equals(user.EasyPassword ?? string.Empty.GetSha1Hash(), authRequest.Password.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase);
                }
            }

            // Update LastActivityDate and LastLoginDate, then save
            if (success)
            {
                user.LastActivityDate = user.LastLoginDate = DateTime.UtcNow;
                await _userManager.UpdateUser(user).ConfigureAwait(false);
                await _userManager.UpdateInvalidLoginAttemptCount(user, 0).ConfigureAwait(false);
            }
            else
            {
                await _userManager.UpdateInvalidLoginAttemptCount(user, user.Policy.InvalidLoginAttemptCount + 1).ConfigureAwait(false);
            }

            _logger.Info("Authentication request for {0} {1}.", user.Name, success ? "has succeeded" : "has been denied");

            return success;
        }
    }
}
