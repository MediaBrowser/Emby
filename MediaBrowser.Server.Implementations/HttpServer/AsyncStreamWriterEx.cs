using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Web;
using MediaBrowser.Controller.Net;

namespace MediaBrowser.Server.Implementations.HttpServer
{
    public class AsyncStreamWriterEx : AsyncStreamWriter, IHttpResult
    {
        /// <summary>
        /// Gets or sets the source stream.
        /// </summary>
        /// <value>The source stream.</value>
        private IAsyncStreamSource _source;

        private IHttpResult _httpResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncStreamWriter" /> class.
        /// </summary>
        public AsyncStreamWriterEx(IAsyncStreamSource source) : base(source)
        {
            _httpResult = source as IHttpResult;

            if (_httpResult == null)
            {
                throw new ArgumentException("AsyncStreamWriterEx: source does not implement IHttpResult");
            }

            _source = source;
        }

        public string ContentType
        {
            get
            {
                return _httpResult.ContentType;
            }
            set
            {
                _httpResult.ContentType = value;
            }
        }

        public List<System.Net.Cookie> Cookies
        {
            get { return _httpResult.Cookies; }
        }

        public Dictionary<string, string> Headers
        {
            get { return _httpResult.Headers; }
        }

        public int PaddingLength
        {
            get
            {
                return _httpResult.PaddingLength;
            }
            set
            {
                _httpResult.PaddingLength = value;
            }
        }

        public IRequest RequestContext
        {
            get
            {
                return _httpResult.RequestContext;
            }
            set
            {
                _httpResult.RequestContext = value;
            }
        }

        public object Response
        {
            get
            {
                return _httpResult.Response;
            }
            set
            {
                _httpResult.Response = value;
            }
        }

        public IContentTypeWriter ResponseFilter
        {
            get
            {
                return _httpResult.ResponseFilter;
            }
            set
            {
                _httpResult.ResponseFilter = value;
            }
        }

        public Func<IDisposable> ResultScope
        {
            get
            {
                return _httpResult.ResultScope;
            }
            set
            {
                _httpResult.ResultScope = value;
            }
        }

        public int Status
        {
            get
            {
                return _httpResult.Status;
            }
            set
            {
                _httpResult.Status = value;
            }
        }

        public System.Net.HttpStatusCode StatusCode
        {
            get
            {
                return _httpResult.StatusCode;
            }
            set
            {
                _httpResult.StatusCode = value;
            }
        }

        public string StatusDescription
        {
            get
            {
                return _httpResult.StatusDescription;
            }
            set
            {
                _httpResult.StatusDescription = value;
            }
        }
    }
}
