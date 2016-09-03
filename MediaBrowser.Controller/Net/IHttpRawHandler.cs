using MediaBrowser.Controller.Net;
using ServiceStack.Web;
using System;

namespace MediaBrowser.Controller.Net
{
    public interface IHttpRawHandler
    {
        /// <summary>
        /// Processes the raw request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns></returns>
        Action<IRequest, IResponse> ProcessRawRequest(IHttpRequest request);
    }
}
