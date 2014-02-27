using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using System;
using System.Linq;

namespace MediaBrowser.Server.Implementations.PlayTo
{
    public class PlayToControllerFactory : ISessionControllerFactory
    {        
        private readonly ISessionManager _sessionManager;
        private readonly IItemRepository _itemRepository;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;        
        private readonly ILogger _logger;
        private readonly string _ipAddress;

        public PlayToControllerFactory(ISessionManager sessionManager, IItemRepository itemRepository, ILibraryManager libraryManager, INetworkManager networkManager,IUserManager userManager, ILogManager logManager)
        {            
            _itemRepository = itemRepository;
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _logger = logManager.GetLogger("PlayTo");
            _ipAddress = networkManager.GetLocalIpAddresses().FirstOrDefault();            
            _userManager = userManager;
        }

        public ISessionController GetSessionController(SessionInfo session)
        {            
            return new PlayToController(session, _sessionManager, _userManager, _itemRepository, _libraryManager, _logger, _ipAddress);
        }
    }
}
