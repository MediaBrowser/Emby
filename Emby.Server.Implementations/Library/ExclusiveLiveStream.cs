using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.System;
using MediaBrowser.Model.LiveTv;
using System.Linq;
using MediaBrowser.Controller.Library;

namespace Emby.Server.Implementations.Library
{
    public class ExclusiveLiveStream : ILiveStream
    {
        public int ConsumerCount { get; set; }
        public string OriginalStreamId { get; set; }

        public string TunerHostId => null;

        public bool EnableStreamSharing { get; set; }
        public MediaSourceInfo MediaSource { get; set; }

        public string UniqueId { get; private set; }

        private ILiveTvService _liveTvService;
        private string _openedId;

        public ExclusiveLiveStream(MediaSourceInfo mediaSource, ILiveTvService liveTvService, string openedId)
        {
            MediaSource = mediaSource;
            EnableStreamSharing = false;
            _liveTvService = liveTvService;
            _openedId = openedId;
            ConsumerCount = 1;
            UniqueId = Guid.NewGuid().ToString("N");
        }

        public Task Close()
        {
            try
            {
                return _liveTvService.CloseLiveStream(_openedId, CancellationToken.None);
            }
            catch (NotImplementedException)
            {
                return Task.CompletedTask;
            }
        }

        public Task Open(CancellationToken openCancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
