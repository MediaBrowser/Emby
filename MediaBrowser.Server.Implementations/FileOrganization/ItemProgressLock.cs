using MediaBrowser.Controller.Localization;
using MediaBrowser.Controller.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.FileOrganization
{
    class ItemProgressLock : IDisposable
    {
        private readonly ConcurrentDictionary<string, bool> _inProgressItemIds;
        private readonly IServerManager _serverManager;
        private string _resultId;

        public ItemProgressLock(string resultId, ConcurrentDictionary<string, bool> inProgressItemIds, IServerManager serverManager, ILocalizationManager localizationManager)
        {
            _inProgressItemIds = inProgressItemIds;
            _serverManager = serverManager;

            if (!_inProgressItemIds.TryAdd(resultId, false))
            {
                throw new ItemInProgressException(localizationManager);
            }

            _resultId = resultId;

            if (_serverManager != null)
            {
                _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => string.Empty, CancellationToken.None);
            }
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(_resultId))
            {
                bool value;
                _inProgressItemIds.TryRemove(_resultId, out value);

                if (_serverManager != null)
                {
                    _serverManager.SendWebSocketMessageAsync("AutoOrganizeUpdate", () => string.Empty, CancellationToken.None);
                }
            }
        }
    }
}
