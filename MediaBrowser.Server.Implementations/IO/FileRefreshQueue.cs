using CommonIO;
using MediaBrowser.Common.Events;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.IO
{
    class FileRefreshQueue
    {
        /// <summary>
        /// Delay in seconds for queue item processing.
        /// </summary>
        private int _queueRetryDelay;

        /// <summary>
        /// The file system abstraction object.
        /// </summary>
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// The timer lock.
        /// </summary>
        /// <remarks>Lock to prevent concurrent timer operations.</remarks>
        private readonly object _timerLock = new object();

        /// <summary>
        /// The queue timer.
        /// </summary>
        private Timer _queueTimer;

        /// <summary>
        ///  The internal queue.
        /// </summary>
        private ConcurrentDictionary<string, FileRefreshItem> _internalQueue = new ConcurrentDictionary<string, FileRefreshItem>();

        /// <summary>
        /// Occurs when an item is ready for processing.
        /// </summary>
        public event EventHandler<FileRefreshEventArgs> ItemReady;

        /// <summary>
        /// Fired when an item is due for processing.
        /// </summary>
        /// <param name="item">The item.</param>
        private bool OnItemReady(FileRefreshItem item)
        {
            var e = new FileRefreshEventArgs(item);

            EventHelper.FireEventIfNotNull(ItemReady, this, e, Logger);

            return !e.Cancel;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileRefreshQueue" /> class.
        /// </summary>
        public FileRefreshQueue(ILogManager logManager, IFileSystem fileSystem, int queueRetryDelay)
        {
            Logger = logManager.GetLogger(GetType().Name);
            _fileSystem = fileSystem;
            _queueRetryDelay = queueRetryDelay;
        }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        private ILogger Logger { get; set; }

        /// <summary>
        /// Adds a path to the refresh queue.
        /// </summary>
        /// <param name="path">A path to a file or directory.</param>
        public void AddPath(string path)
        {
            string folder;
            string filePath;

            var fsMeta =_fileSystem.GetFileSystemInfo(path);

            if (!fsMeta.IsDirectory)
            {
                folder = fsMeta.DirectoryName.ToLower();
                filePath = fsMeta.FullName.ToLower();
            }
            else
            {
                folder = fsMeta.FullName.ToLower();
                filePath = string.Empty;
            }

            var item = _internalQueue.GetOrAdd(folder, new FileRefreshItem(folder));

            if (!string.IsNullOrEmpty(filePath))
            {
                // Avoid implicitly captured closure
                var path2 = path;
                item.FilePaths.TryAdd(path, path2);
            }

            item.DueDate = DateTime.Now.AddSeconds(_queueRetryDelay);

            UpdateTimer();
        }

        /// <summary>
        /// Re-inserts an item into the queue that could not be processed successfully.
        /// </summary>
        /// <param name="newItem"></param>
        private void ReScheduleItem(FileRefreshItem newItem)
        {
            var item = _internalQueue.GetOrAdd(newItem.Folder, newItem);

            if (item != newItem)
            {
                // Merge files from newItem with existing item's files
                foreach (var path in newItem.FilePaths.Keys)
                {
                    item.FilePaths.TryAdd(path, path.ToString());
                }
            }

            // Postpone processing, no matter if new or updated
            item.DueDate = DateTime.Now.AddSeconds(_queueRetryDelay);

            UpdateTimer();
        }

        /// <summary>
        /// Resets the timer to the time of the earliest due date of all items in the queue.
        /// </summary>
        private void UpdateTimer()
        {
            if (_internalQueue.Count == 0)
            {
                DisposeTimer();
                return;
            }

            var nextTimerTime = _internalQueue.Min(e => e.Value.DueDate);
            var nextTimerSpan = nextTimerTime.AddSeconds(1).Subtract(DateTime.Now);

            if (nextTimerSpan < TimeSpan.FromSeconds(1))
            {
                nextTimerSpan = TimeSpan.FromSeconds(1);
            }

            SetTimer(nextTimerSpan);
        }

        /// <summary>
        /// Clears the queue and disables the timer.
        /// </summary>
        public void Clear()
        {
            DisposeTimer();
            _internalQueue.Clear();
        }

        /// <summary>
        /// Sets the timer to fire once as soon as the interval specified by <paramref name="span"/> has elapsed.
        /// </summary>
        /// <param name="span"></param>
        private void SetTimer(TimeSpan span)
        {
            lock (_timerLock)
            {
                if (_queueTimer == null)
                {
                    _queueTimer = new Timer(TimerTick, null, span, TimeSpan.FromMilliseconds(-1));
                }
                else
                {
                    _queueTimer.Change(span, TimeSpan.FromMilliseconds(-1));
                }
            }
        }

        private void DisposeTimer()
        {
            lock (_timerLock)
            {
                if (_queueTimer != null)
                {
                    _queueTimer.Dispose();
                    _queueTimer = null;
                }
            }
        }

        private void TimerTick(object stateInfo)
        {
            var itemFirst = _internalQueue.Where(i => i.Value.DueDate < DateTime.Now).OrderBy(e => e.Value.DueDate).FirstOrDefault();

            FileRefreshItem item;
            
            if (itemFirst.Key != null && _internalQueue.TryRemove(itemFirst.Key, out item))
            {
                if (!this.OnItemReady(item))
                {
                    this.ReScheduleItem(item);
                }
            }

            UpdateTimer();
        }

    }
}
