using MediaBrowser.Model.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using MediaBrowser.Controller.Net;
using System.Collections.Generic;
using ServiceStack.Web;
using System.Net;
using System.Globalization;

namespace MediaBrowser.Api.Playback.Progressive
{
    public class ProgressiveRangeWriter : IAsyncStreamSource, IHttpResult
    {
        private readonly IFileSystem _fileSystem;
        private readonly TranscodingJob _job;
        private readonly ILogger _logger;
        private readonly string _path;
        private readonly CancellationToken _cancellationToken;
        private readonly Dictionary<string, string> _outputHeaders;

        /// <summary>
        /// The us culture
        /// </summary>
        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");

        /// <summary>
        /// The _requested ranges
        /// </summary>
        private List<KeyValuePair<long, long?>> _requestedRanges;

        // 256k
        private const int BufferSize = 81920;

        private long _bytesWritten = 0;
        private long? _contentLength = 0;

        public ProgressiveRangeWriter(StreamState state, string rangeHeader, IFileSystem fileSystem, long? contentLength, Dictionary<string, string> outputHeaders, TranscodingJob job, ILogger logger, CancellationToken cancellationToken)
        {
            RangeHeader = rangeHeader;
            _fileSystem = fileSystem;
            _path = state.OutputFilePath;
            _contentLength = contentLength;
            _outputHeaders = outputHeaders;
            _job = job;
            _logger = logger;
            _cancellationToken = cancellationToken;

            var plainFileName = Path.GetFileNameWithoutExtension(_path);

            _outputHeaders["Cache-Control"] = "public, max-age=86400"; // 1 day
            _outputHeaders["Last-Modified"] = job.CreatedAtUtc.ToString("R", DateTimeFormatInfo.InvariantInfo);
            _outputHeaders["Expires"] = job.CreatedAtUtc.AddDays(1).ToString("R", DateTimeFormatInfo.InvariantInfo);
            _outputHeaders["ETag"] = string.Format("\"{0}\"", plainFileName);

            if (state.RunTimeTicks.HasValue)
            {
                var duration = TimeSpan.FromTicks(state.RunTimeTicks.Value).TotalSeconds;
                _outputHeaders["Content-Duration"] = duration.ToString(CultureInfo.InvariantCulture);
                _outputHeaders["X-Content-Duration"] = duration.ToString(CultureInfo.InvariantCulture);
            }

            SetRangeValues();
            StatusCode = HttpStatusCode.PartialContent;

            Cookies = new List<Cookie>();
            ContentType = outputHeaders["Content-Type"];
        }

        public Func<IDisposable> ResultScope { get; set; }
        public List<Cookie> Cookies { get; private set; }

        public IDictionary<string, string> Options
        {
            get
            {
                return _outputHeaders;
            }
        }

        public Dictionary<string, string> Headers
        {
            get
            {
                return _outputHeaders;
            }
        }

        public string ContentType { get; set; }

        public IRequest RequestContext { get; set; }

        public object Response { get; set; }

        public IContentTypeWriter ResponseFilter { get; set; }

        public int Status { get; set; }

        public HttpStatusCode StatusCode
        {
            get { return (HttpStatusCode)Status; }
            set { Status = (int)value; }
        }

        public string StatusDescription { get; set; }

        public int PaddingLength { get; set; }

        private string RangeHeader { get; set; }

        private long RangeStart { get; set; }
        private long? RangeEnd { get; set; }
        private long? RangeLength { get; set; }

        /// <summary>
        /// Gets the requested ranges.
        /// </summary>
        /// <value>The requested ranges.</value>
        protected List<KeyValuePair<long, long?>> RequestedRanges
        {
            get
            {
                if (_requestedRanges == null)
                {
                    _requestedRanges = new List<KeyValuePair<long, long?>>();

                    // Example: bytes=0-,32-63
                    var ranges = RangeHeader.Split('=')[1].Split(',');

                    foreach (var range in ranges)
                    {
                        var vals = range.Split('-');

                        long start = 0;
                        long? end = null;

                        if (!string.IsNullOrEmpty(vals[0]))
                        {
                            start = long.Parse(vals[0], UsCulture);
                        }
                        if (!string.IsNullOrEmpty(vals[1]))
                        {
                            end = long.Parse(vals[1], UsCulture);
                        }

                        _requestedRanges.Add(new KeyValuePair<long, long?>(start, end));
                    }
                }

                return _requestedRanges;
            }
        }

        public async Task WriteToAsync(Stream outputStream)
        {
            if (StatusCode == HttpStatusCode.PartialContent)
            {
                try
                {
                    var eofCount = 0;

                    using (var fs = _fileSystem.GetFileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, true))
                    {
                        if (RangeStart > 0)
                        {
                            fs.Position = RangeStart;
                        }

                        while (eofCount < 15)
                        {
                            var bytesRead = await CopyToAsyncInternal(fs, outputStream, BufferSize, _cancellationToken).ConfigureAwait(false);

                            //var position = fs.Position;
                            //_logger.Debug("Streamed {0} bytes to position {1} from file {2}", bytesRead, position, path);

                            if (bytesRead == 0)
                            {
                                if (_job == null || _job.HasExited)
                                {
                                    eofCount++;
                                }
                                await Task.Delay(100, _cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                eofCount = 0;
                            }
                        }
                    }
                }
                finally
                {
                    if (_job != null)
                    {
                        ApiEntryPoint.Instance.OnTranscodeEndRequest(_job);
                    }
                }
            }
        }

        private async Task<int> CopyToAsyncInternal(Stream source, Stream destination, Int32 bufferSize, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            int totalBytesRead = 0;

            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);

                _bytesWritten += bytesRead;
                totalBytesRead += bytesRead;

                if (_job != null)
                {
                    _job.BytesDownloaded = Math.Max(_job.BytesDownloaded ?? _bytesWritten, _bytesWritten);
                }
            }

            return totalBytesRead;
        }

        /// <summary>
        /// Sets the range values.
        /// </summary>
        private void SetRangeValues()
        {
            if (RequestedRanges.Count != 1)
            {
                StatusCode = HttpStatusCode.RequestedRangeNotSatisfiable;
                return;
            }

            var requestedRange = RequestedRanges[0];

            if (_job.HasExited && _job.CompletionPercentage.HasValue && _job.CompletionPercentage.Value > 99.0)
            {
                var fileInfo = _fileSystem.GetFileInfo(_path);
                _contentLength = fileInfo.Length;
            }
            ////else if (_job.BitRate.HasValue)
            ////{

            ////}

            RangeStart = requestedRange.Key;

            if (requestedRange.Value.HasValue)
            {
                RangeEnd = requestedRange.Value.Value;
            }
            else if (!_contentLength.HasValue)
            {
                // In case we don't know the total length we must still return a defined range
                // so we return what we got so far
                var fileInfo = _fileSystem.GetFileInfo(_path);
                RangeEnd = fileInfo.Length - 1;
            }
            else
            {
                RangeEnd = _contentLength.Value - 1;
            }

            RangeLength = RangeEnd - RangeStart + 1;

            string totalLength = _contentLength.HasValue ? _contentLength.Value.ToString(UsCulture) : "*";

            // Content-Length is the length of what we're serving, not the original content
            _outputHeaders["Content-Length"] = RangeLength.Value.ToString(UsCulture);
            _outputHeaders["Content-Range"] = string.Format("bytes {0}-{1}/{2}", RangeStart, RangeEnd, totalLength);
        }
    }
}
