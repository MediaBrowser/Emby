using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Server.Implementations.PlayTo.Configuration;
using MediaBrowser.Server.Implementations.PlayTo.Helpers;
using MediaBrowser.Server.Implementations.PlayTo.Managed;
using MediaBrowser.Server.Implementations.PlayTo.Managed.Entities;
using MediaBrowser.Server.Implementations.PlayTo.Managed.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace MediaBrowser.Server.Implementations.PlayTo
{
    public class PlayToController : ISessionController
    {
        #region Fields

        private Device _device;
        private BaseItem _currentItem = null;
        private TranscodeSettings[] _transcodeSettings;
        private readonly string _ipAddress;
        private readonly SessionInfo _session;
        private readonly ISessionManager _sessionManager;
        private readonly IItemRepository _itemRepository;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly ILogger _logger;
        int _port;       
        private bool playbackStarted = false;
        private Guid _defaultUserId;

        #endregion

        #region Properties

        public bool SupportsMediaRemoteControl
        {
            get { return true; }
        }

        public bool IsSessionActive
        {
            get 
            {
                if (_device == null || _device.UpdateTime == null)
                    return false;
                return DateTime.UtcNow <= _device.UpdateTime.AddSeconds(30); 
            }
        }

        #endregion

        #region Constructor & Initializer

        public PlayToController(SessionInfo session, ISessionManager sessionManager, IUserManager userManager, IItemRepository itemRepository, ILibraryManager libraryManager, ILogger logger, string ipAddress)
        {           
            _session = session;
            _itemRepository = itemRepository;
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _logger = logger;
            _ipAddress = ipAddress;
            _port = 8096; //TODO Luke, provide the correct API port            
            _defaultUserId = _session.UserId.Value;
        }

        public void Init(Device device, TranscodeSettings[] transcodeSettings)
        {
            _transcodeSettings = transcodeSettings;
            _device = device;            
            _device.PlaybackChanged += Device_PlaybackChanged;
            _device.CurrentIdChanged += Device_CurrentIdChanged;
            _device.Start();

            _updateTimer = new Timer(1000);
            _updateTimer.Elapsed += updateTimer_Elapsed;         
            _updateTimer.Start();
        }    

        #endregion
        
        #region Device EventHandlers & Update Timer

        Timer _updateTimer;

        async void Device_PlaybackChanged(object sender, TransportStateEventArgs e)
        {
            if (_currentItem == null)
                return;
            if (e.Stopped == false)
                await ReportProgress();
            else if (e.Stopped && playbackStarted)
            {
                playbackStarted = false;
                await _sessionManager.OnPlaybackStopped(new PlaybackStopInfo { Item = _currentItem, SessionId = _session.Id, PositionTicks = _device.Position.Ticks });
                await SetNext();

            }
        }

        async void Device_CurrentIdChanged(object sender, CurrentIdEventArgs e)
        {
            if (e.Id != null && e.Id != Guid.Empty)
            {
                if (_currentItem != null && _currentItem.Id == e.Id)
                {
                    return;
                }
                try
                {
                    var item = _libraryManager.GetItemById(e.Id);

                    if (item != null)
                    {
                        _logger.Log(LogSeverity.Debug, "{0} - CurrentId {1}", this._session.DeviceName, item.Id);
                        _currentItem = item;
                        playbackStarted = false;
                        await ReportProgress();
                    }
                }
                catch
                {
                    _logger.Log(LogSeverity.Debug, "{0} - libraryManager.GetItemById failed . ItemId: {1}", this._session.DeviceName, e.Id);
                }
            }

        }

        /// <summary>
        /// Handles the Elapsed event of the updateTimer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ElapsedEventArgs"/> instance containing the event data.</param>
        async void updateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            ((Timer)sender).Stop();
            
            await ReportProgress();

            if (!_disposed && IsSessionActive)
                ((Timer)sender).Start();
        }

        /// <summary>
        /// Reports the playback progress.
        /// </summary>
        /// <returns></returns>
        private async Task ReportProgress()
        {
            if (_currentItem == null || _device.IsStopped)
                return;

            if (!playbackStarted)
            {
                await _sessionManager.OnPlaybackStart(new PlaybackInfo { Item = _currentItem, SessionId = _session.Id, CanSeek = true, QueueableMediaTypes = new List<string> { "Audio", "Video" } });
                playbackStarted = true;
            }

            if ((_device.IsPlaying || _device.IsPaused) && _device.Position != null)
            {
                var playlistItem = Playlist.Where(p => p.PlayState == 1).FirstOrDefault();
                if (playlistItem != null && playlistItem.Transcode)
                {
                    await _sessionManager.OnPlaybackProgress(new PlaybackProgressInfo { Item = _currentItem, SessionId = _session.Id, PositionTicks = _device.Position.Ticks + playlistItem.StartPositionTicks, IsMuted = _device.IsMuted, IsPaused = _device.IsPaused });
                }
                else if (_currentItem != null)
                {
                    await _sessionManager.OnPlaybackProgress(new PlaybackProgressInfo { Item = _currentItem, SessionId = _session.Id, PositionTicks = _device.Position.Ticks, IsMuted = _device.IsMuted, IsPaused = _device.IsPaused });
                }
            }
        }

        #endregion

        #region SendCommands

        public System.Threading.Tasks.Task SendPlayCommand(Model.Session.PlayRequest command, System.Threading.CancellationToken cancellationToken)
        {
            _logger.Debug("{0} - Received PlayRequest: {1}", this._session.DeviceName, command.PlayCommand);

            List<BaseItem> items = new List<BaseItem>();
            foreach (string id in command.ItemIds)
            {
                AddItemFromId(Guid.Parse(id), items);
            }


            List<PlaylistItem> playlist = new List<PlaylistItem>();
            bool isFirst = true;

            foreach (BaseItem item in items)
            {
                if (isFirst && command.StartPositionTicks.HasValue)
                {
                    playlist.Add(CreatePlaylistItem(item, command.StartPositionTicks.Value));
                    isFirst = false;
                }
                else
                {
                    playlist.Add(CreatePlaylistItem(item, 0));
                }
            }

            _logger.Debug( "{0} - Playlist created", this._session.DeviceName);

            if (command.PlayCommand == Model.Session.PlayCommand.PlayLast)
            {
                AddItemsToPlaylist(playlist);
                return Task.FromResult(true);
            }
            if (command.PlayCommand == Model.Session.PlayCommand.PlayNext)
            {
                AddItemsToPlaylist(playlist);
                return Task.FromResult(true);
            }

            _logger.Debug("{0} - Playing {1} items", this._session.DeviceName, playlist.Count);
            return PlayItems(playlist);
        }

        public System.Threading.Tasks.Task SendPlaystateCommand(Model.Session.PlaystateRequest command, System.Threading.CancellationToken cancellationToken)
        {
            switch (command.Command)
            {
                case Model.Session.PlaystateCommand.Stop:
                    Playlist.Clear();
                    return _device.SetStop();

                case Model.Session.PlaystateCommand.Pause:
                    return _device.SetPause();

                case Model.Session.PlaystateCommand.Unpause:
                    return _device.SetPlay();

                case Model.Session.PlaystateCommand.Seek:
                    var playlistItem = Playlist.Where(p => p.PlayState == 1).FirstOrDefault();
                    if (playlistItem != null && playlistItem.Transcode && playlistItem.IsVideo && _currentItem != null)
                    {
                        var newItem = CreatePlaylistItem(_currentItem, command.SeekPositionTicks.Value);
                        playlistItem.StartPositionTicks = newItem.StartPositionTicks;
                        playlistItem.StreamUrl = newItem.StreamUrl;
                        playlistItem.Didl = newItem.Didl;
                        return _device.SetAvTransport(playlistItem.StreamUrl, playlistItem.DlnaHeaders, playlistItem.Didl);

                    }
                    return _device.Seek(TimeSpan.FromTicks(command.SeekPositionTicks.Value));


                case Model.Session.PlaystateCommand.NextTrack:
                    _currentItem = null;
                    return SetNext();

                case Model.Session.PlaystateCommand.PreviousTrack:
                    _currentItem = null;
                    return SetPrevious();
            }

            return Task.FromResult(true);
        }

        public System.Threading.Tasks.Task SendSystemCommand(Model.Session.SystemCommand command, System.Threading.CancellationToken cancellationToken)
        {
            switch (command)
            {
                case Model.Session.SystemCommand.VolumeDown:
                    return _device.VolumeDown();
                case Model.Session.SystemCommand.VolumeUp:
                    return _device.VolumeUp();
                case Model.Session.SystemCommand.Mute:
                    return _device.VolumeDown(true);
                case Model.Session.SystemCommand.Unmute:
                    return _device.VolumeUp(true);
                case Model.Session.SystemCommand.ToggleMute:
                    return _device.ToggleMute();
                default:
                    return Task.FromResult(true);
            }
        }

        public System.Threading.Tasks.Task SendUserDataChangeInfo(Model.Session.UserDataChangeInfo info, System.Threading.CancellationToken cancellationToken)
        {          
            return Task.FromResult(true);
        }

        #region Not yet supported

        public System.Threading.Tasks.Task SendRestartRequiredNotification(System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public System.Threading.Tasks.Task SendServerRestartNotification(System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public System.Threading.Tasks.Task SendServerShutdownNotification(System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public System.Threading.Tasks.Task SendBrowseCommand(Model.Session.BrowseRequest command, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public System.Threading.Tasks.Task SendLibraryUpdateInfo(Model.Entities.LibraryUpdateInfo info, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public System.Threading.Tasks.Task SendMessageCommand(Model.Session.MessageCommand command, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        #endregion

        #endregion

        #region Playlist

        private List<PlaylistItem> _playlist = new List<PlaylistItem>();

        private List<PlaylistItem> Playlist
        {
            get
            {
                return _playlist;
            }
            set
            {
                _playlist = value;
            }
        }

        private void AddItemFromId(Guid id, List<BaseItem> list)
        {
            var item = _libraryManager.GetItemById(id);
            if (item.IsFolder)
            {
                foreach (Guid childId in _itemRepository.GetChildren(item.Id))
                {
                    AddItemFromId(childId, list);
                }
            }
            else
            {
                if (item.MediaType == MediaType.Audio || item.MediaType == MediaType.Video)
                {
                    list.Add(item);
                }
            }
        }

        private PlaylistItem CreatePlaylistItem(BaseItem item, long startPostionTicks)
        {
            var streams = _itemRepository.GetMediaStreams(new MediaStreamQuery { ItemId = item.Id });

            var playlistItem = PlaylistItem.GetBasicConfig(item, _transcodeSettings);
            playlistItem.StartPositionTicks = startPostionTicks;

            if (playlistItem.IsAudio)
                playlistItem.StreamUrl = StreamHelper.GetAudioUrl(playlistItem, _ipAddress, _port);
            else
                playlistItem.StreamUrl = StreamHelper.GetVideoUrl(_device.Properties, playlistItem, streams, _ipAddress, _port);

            var didl = DidlBuilder.Build(item, _session.UserId.ToString(), _ipAddress, _port, playlistItem.StreamUrl, streams);
            playlistItem.Didl = didl;

            string header = StreamHelper.GetDlnaHeaders(playlistItem);
            playlistItem.DlnaHeaders = header;
            return playlistItem;
        }

        /// <summary>
        /// Plays the items.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <returns></returns>
        private async Task<bool> PlayItems(IEnumerable<PlaylistItem> items)
        {
            Playlist.Clear();
            Playlist.AddRange(items);
            await SetNext();
            return true;
        }

        /// <summary>
        /// Adds the items to playlist.
        /// </summary>
        /// <param name="items">The items.</param>
        private void AddItemsToPlaylist(IEnumerable<PlaylistItem> items)
        {
            Playlist.AddRange(items);
        }

        private async Task<bool> SetNext()
        {
            if (!Playlist.Any() || !Playlist.Where(i => i.PlayState == 0).Any())
            {
                return true;
            }
            var currentitem = Playlist.Where(i => i.PlayState == 1).FirstOrDefault();

            if (currentitem != null)
            {
                currentitem.PlayState = 2;
            }

            var nextTrack = Playlist.Where(i => i.PlayState == 0).FirstOrDefault();
            if (nextTrack == null)
            {
                await _device.SetStop();
                return true;
            }
            nextTrack.PlayState = 1;
            await _device.SetAvTransport(nextTrack.StreamUrl, nextTrack.DlnaHeaders, nextTrack.Didl);
            if (nextTrack.StartPositionTicks > 0 && !nextTrack.Transcode)
                await _device.Seek(TimeSpan.FromTicks(nextTrack.StartPositionTicks));
            return true;
        }

        public Task<bool> SetPrevious()
        {
            if (!Playlist.Any() || Playlist.Where(i => i.PlayState == 2).Count() == 0)
                return Task.FromResult(false);

            var currentitem = Playlist.Where(i => i.PlayState == 1).FirstOrDefault();

            var prevTrack = Playlist.Where(i => i.PlayState == 2).LastOrDefault();

            if (currentitem != null)
            {
                currentitem.PlayState = 0;
            }

            if (prevTrack == null)
                return Task.FromResult(false);

            prevTrack.PlayState = 1;
            return _device.SetAvTransport(prevTrack.StreamUrl, prevTrack.DlnaHeaders, prevTrack.Didl);
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {            
            if (!_disposed)
            {
                _updateTimer.Stop();
                _disposed = true;                
                _device.Dispose();
                _logger.Log(LogSeverity.Debug, "PlayTo - Controller disposed");
            }
        }

        #endregion
    }
}
