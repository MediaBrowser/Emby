using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Server.Implementations.PlayTo;
using MediaBrowser.Server.Implementations.PlayTo.Configuration;
using MediaBrowser.Server.Implementations.PlayTo.Extensions;
using MediaBrowser.Server.Implementations.PlayTo.Managed;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.EntryPoints
{
    public class PlayToServerEntryPoint : IServerEntryPoint
    {
        #region Fields

        const string DEFAULT_USER = "PlayTo";

        private bool _disposed = false;
        
        private IUserManager _userManager;        
        private IXmlSerializer _xmlSerializer;        
        private PlayToManager _manager;
        
        #endregion

        public PlayToServerEntryPoint(ILogManager logManager, ISessionManager sessionManager, IUserManager userManager, IXmlSerializer xmlSerializer)
        {
            _manager = new PlayToManager(logManager.GetLogger("PlayTo"), sessionManager);
            
            _userManager = userManager;
            _xmlSerializer = xmlSerializer;            
        }

        /// <summary>
        /// Creates the defaultuser if needed.
        /// </summary>
        private async Task<User> CreateUserIfNeeded()
        {
            var user = _userManager.Users.Where(u => u.Name == DEFAULT_USER).FirstOrDefault();
            if (user == null)
            {

                user = await _userManager.CreateUser(DEFAULT_USER);
                user.Configuration.IsHidden = true;
                user.Configuration.IsAdministrator = false;
                user.SaveConfiguration(_xmlSerializer);
                await _userManager.UpdateUser(user);

            }
            return user;
        }

        public async void Run()
        {
            var defaultUser = await CreateUserIfNeeded();
            _manager.Start(defaultUser);            
        }
     
        #region Dispose

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _manager.Stop();
                _manager.Dispose();
            }
        }

        #endregion
    }
}
