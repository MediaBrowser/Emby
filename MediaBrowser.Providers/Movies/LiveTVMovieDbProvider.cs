using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Movies
{
    public class LiveTvMovieDbProvider : IRemoteMetadataProvider<LiveTvProgram, LiveTvMovieInfo>, IDisposable, IHasOrder
    {
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(LiveTvMovieInfo searchInfo, CancellationToken cancellationToken)
        {
            return MovieDbProvider.Current.GetMovieSearchResults(searchInfo, cancellationToken);
        }

        public Task<MetadataResult<LiveTvProgram>> GetMetadata(LiveTvMovieInfo info, CancellationToken cancellationToken)
        {
            return MovieDbProvider.Current.GetItemMetadata<LiveTvProgram>(info, cancellationToken);
        }

        public string Name
        {
            get { return "LiveTvMovieDbProvider"; }
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return MovieDbProvider.Current.GetImageResponse(url, cancellationToken);
        }

        public void Dispose()
        {
        }

        public int Order
        {
            get { return 1; }
        }
    }
}