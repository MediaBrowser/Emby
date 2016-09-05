using MediaBrowser.Controller.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Users;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Net;
using MediaBrowser.Common.Events;
using MediaBrowser.Common;

namespace MediaBrowser.Server.Implementations.Security
{
    class AuthenticationManager : IAuthenticationManager
    {
        public event EventHandler<GenericEventArgs<AuthenticationRequest>> AuthenticationFailed;
        public event EventHandler<GenericEventArgs<AuthenticationRequest>> AuthenticationSucceeded;

        private readonly IUserManager _userManager;
        private readonly IDeviceManager _deviceManager;
        private readonly IApplicationHost _appHost;
        private readonly ILogger _logger;

        private readonly List<IAuthenticationProvider> _authProviders = new List<IAuthenticationProvider>();

        public AuthenticationManager(IUserManager userManager, IDeviceManager deviceManager, IApplicationHost appHost,ILogger logger)
        {
            _userManager = userManager;
            _deviceManager = deviceManager;
            _appHost = appHost;
            _logger = logger;
        }
        public async Task<AuthenticationResult> Authenticate(AuthenticationRequest request)
        {
            var user = _userManager.GetUserByName(request.Username);

            if (user != null && !string.IsNullOrWhiteSpace(request.DeviceId))
            {
                if (!_deviceManager.CanAccessDevice(user.Id.ToString("N"), request.DeviceId))
                {
                    throw new SecurityException("User is not allowed access from this device.");
                }
            }

            var result = false;

            foreach(var authProvider in _authProviders)
            {
                result = (await authProvider.Authenticate(request).ConfigureAwait(false)) || result;
            }

            if (!result)
            {
                EventHelper.FireEventIfNotNull(AuthenticationFailed, this, new GenericEventArgs<AuthenticationRequest>(request), _logger);

                throw new SecurityException("Invalid user or password entered.");
            }

            EventHelper.FireEventIfNotNull(AuthenticationSucceeded, this, new GenericEventArgs<AuthenticationRequest>(request), _logger);

            return new AuthenticationResult
            {
                User = _userManager.GetUserDto(user, request.RemoteEndPoint),
                ServerId = _appHost.SystemId
            };
        }
    }
}
