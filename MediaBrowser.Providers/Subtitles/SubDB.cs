using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Common;
using System;
using MediaBrowser.Model.Net;

namespace MediaBrowser.Providers.Subtitles
{
    public class SubDB : ISubtitleProvider, IHasOrder
    {
        private readonly IFileSystem _fileSystem;
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IApplicationHost _appHost;

        public SubDB(ILogger logger, IHttpClient httpClient, IFileSystem fileSystem, IApplicationHost appHost)
        {
            _logger = logger;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _appHost = appHost;
        }

        private HttpRequestOptions BaseRequestOptions => new HttpRequestOptions
        {
            UserAgent =
                $"SubDB/1.0 (Emby/{_appHost.ApplicationVersion}; https://github.com/MediaBrowser/Emby)"
        };

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var opts = BaseRequestOptions;
            opts.Url = "http://api.thesubdb.com/?action=download&hash=" + id;
            _logger.Debug("Requesting {0}", opts.Url);

            using (var response = await _httpClient.GetResponse(opts).ConfigureAwait(false))
            {
                var ms = new MemoryStream();

                await response.Content.CopyToAsync(ms).ConfigureAwait(false);
                ms.Position = 0;

                string cd = null;
                response.Headers.TryGetValue("Content-Disposition", out cd);

                var fileExt = (cd ?? string.Empty).Split('.').LastOrDefault();

                if (string.IsNullOrWhiteSpace(fileExt))
                {
                    fileExt = "srt";
                }

                return new SubtitleResponse
                {
                    Format = fileExt,
                    Language = id.Split('=').LastOrDefault(),
                    Stream = ms
                };
            }
        }

        public string Name => "SubDB";

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request,
            CancellationToken cancellationToken)
        {
            var hash = await GetHash(request.MediaPath, cancellationToken);
            var opts = BaseRequestOptions;
            opts.Url = "http://api.thesubdb.com/?action=search&hash=" + hash;
            _logger.Debug("Requesting {0}", opts.Url);

            try
            {
                using (var response = await _httpClient.GetResponse(opts).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(response.Content))
                    {
                        var result = await reader.ReadToEndAsync().ConfigureAwait(false);
                        _logger.Debug("Search for subtitles for {0} returned {1}", hash, result);
                        return result
                            .Split(',')
                            .Where(lang => string.Equals(request.TwoLetterISOLanguageName, lang, StringComparison.OrdinalIgnoreCase)) //TODO: use three letter code
                            .Select(lang => new RemoteSubtitleInfo
                            {
                                IsHashMatch = true,
                                ProviderName = Name,
                                Id = $"{hash}&language={lang}",
                                Name = "A subtitle matched by hash"

                            }).ToList();
                    }
                }
            }
            catch (HttpException ex)
            {
                if (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.NotFound)
                {
                    return new List<RemoteSubtitleInfo>();
                }

                throw;
            }
        }

        public IEnumerable<VideoContentType> SupportedMediaTypes =>
            new List<VideoContentType> { VideoContentType.Episode, VideoContentType.Movie };

        public int Order => 1;

        /// <summary>
        ///     Reads 64*1024 bytes from the start and the end of the file, combines them and returns its MD5 hash
        /// </summary>
        private async Task<string> GetHash(string path, CancellationToken cancellationToken)
        {
            const int readSize = 64 * 1024;
            var buffer = new byte[readSize * 2];
            _logger.Debug("Reading {0}", path);
            using (var stream =
                _fileSystem.GetFileStream(path, FileOpenMode.Open, FileAccessMode.Read, FileShareMode.Read))
            {
                await stream.ReadAsync(buffer, 0, readSize, cancellationToken);
                stream.Seek(-readSize, SeekOrigin.End);
                await stream.ReadAsync(buffer, readSize, readSize, cancellationToken);
            }

            var hash = new StringBuilder();
            using (var md5 = MD5.Create())
            {
                foreach (var b in md5.ComputeHash(buffer))
                    hash.Append(b.ToString("X2"));
            }
            _logger.Debug("Computed hash {0} of {1}", hash.ToString(), path);
            return hash.ToString();
        }
    }
}