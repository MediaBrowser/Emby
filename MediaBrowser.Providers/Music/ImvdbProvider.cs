using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Providers.Manager;
using System.Linq;
using MediaBrowser.Controller.IO;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Providers;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Music
{
    public class ImvdbProvider : IRemoteMetadataProvider<MusicVideo, MusicVideoInfo>
    {
        public string Name => "IMVDb";
        private IHttpClient _httpClient;
        private ILogger _logger;

        public ImvdbProvider(IHttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<MetadataResult<MusicVideo>> GetMetadata(MusicVideoInfo info, CancellationToken cancellationToken)
        {
            var imvdbId = info.GetProviderId("IMVDb");

            if (string.IsNullOrEmpty(imvdbId))
            {
                var searchResults = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
                var searchResult = searchResults.FirstOrDefault();
                if (searchResult != null)
                {
                    imvdbId = searchResult.GetProviderId("IMVDb");
                }
            }

            var result = new MetadataResult<MusicVideo>();

            if (!string.IsNullOrEmpty(imvdbId))
            {
                // do lookup here by imvdb id
                result.HasMetadata = true;
                // set properties from data
                result.Item.Overview = "abc";
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MusicVideoInfo searchInfo, CancellationToken cancellationToken)
        {
            return new List<RemoteSearchResult>();
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url
            });
        }
    }
}
