using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.HttpServer;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;
using System.Text;
using System.Globalization;

namespace Emby.Server.Implementations.Services
{
    public class ServiceHandler
    {
        protected static Task<object> CreateContentTypeRequest(HttpListenerHost host, IRequest httpReq, Type requestType, string contentType)
        {
            if (!string.IsNullOrEmpty(contentType) && httpReq.ContentLength > 0)
            {
                var deserializer = RequestHelper.GetRequestReader(host, contentType);
                if (deserializer != null)
                {
                    return deserializer(requestType, httpReq.InputStream);
                }
            }
            return Task.FromResult(host.CreateInstance(requestType)); 
        }

        public static RestPath FindMatchingRestPath(string httpMethod, string pathInfo, ILogger logger, out string contentType)
        {
            pathInfo = GetSanitizedPathInfo(pathInfo, out contentType);

            return ServiceController.Instance.GetRestPathForRequest(httpMethod, pathInfo, logger);
        }

        public static string GetSanitizedPathInfo(string pathInfo, out string contentType)
        {
            contentType = null;
            var pos = pathInfo.LastIndexOf('.');
            if (pos >= 0)
            {
                var format = pathInfo.Substring(pos + 1);
                contentType = GetFormatContentType(format);
                if (contentType != null)
                {
                    pathInfo = pathInfo.Substring(0, pos);
                }
            }
            return pathInfo;
        }

        private static string GetFormatContentType(string format)
        {
            //built-in formats
            if (format == "json")
                return "application/json";
            if (format == "xml")
                return "application/xml";

            return null;
        }

        public RestPath GetRestPath(string httpMethod, string pathInfo)
        {
            if (this.RestPath == null)
            {
                string contentType;
                this.RestPath = FindMatchingRestPath(httpMethod, pathInfo, new NullLogger(), out contentType);

                if (contentType != null)
                    ResponseContentType = contentType;
            }
            return this.RestPath;
        }

        public RestPath RestPath { get; set; }

        // Set from SSHHF.GetHandlerForPathInfo()
        public string ResponseContentType { get; set; }

        public async Task ProcessRequestAsync(HttpListenerHost appHost, IRequest httpReq, IResponse httpRes, ILogger logger, string operationName, CancellationToken cancellationToken)
        {
            var restPath = GetRestPath(httpReq.Verb, httpReq.PathInfo);
            if (restPath == null)
            {
                throw new NotSupportedException("No RestPath found for: " + httpReq.Verb + " " + httpReq.PathInfo);
            }

            SetRoute(httpReq, restPath);

            if (ResponseContentType != null)
                httpReq.ResponseContentType = ResponseContentType;

            var request = httpReq.Dto = await CreateRequest(appHost, httpReq, restPath, logger).ConfigureAwait(false);

            appHost.ApplyRequestFilters(httpReq, httpRes, request);

            var response = await appHost.ServiceController.Execute(appHost, request, httpReq).ConfigureAwait(false);

            FilterResponse(httpReq, httpRes, response);

            await ResponseHelper.WriteToResponse(httpRes, httpReq, response, cancellationToken).ConfigureAwait(false);
        }

        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");

        /// <summary>
        /// Filters the response.
        /// </summary>
        /// <param name="req">The req.</param>
        /// <param name="res">The res.</param>
        /// <param name="dto">The dto.</param>
        private void FilterResponse(IRequest req, IResponse res, object dto)
        {
            // Try to prevent compatibility view
            //res.AddHeader("X-UA-Compatible", "IE=Edge");
            res.AddHeader("Access-Control-Allow-Headers", "Accept, Accept-Language, Authorization, Cache-Control, Content-Disposition, Content-Encoding, Content-Language, Content-Length, Content-MD5, Content-Range, Content-Type, Date, Host, If-Match, If-Modified-Since, If-None-Match, If-Unmodified-Since, Origin, OriginToken, Pragma, Range, Slug, Transfer-Encoding, Want-Digest, X-MediaBrowser-Token, X-Emby-Authorization");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, PATCH, OPTIONS");
            res.AddHeader("Access-Control-Allow-Origin", "*");

            var exception = dto as Exception;

            if (exception != null)
            {
                //_logger.ErrorException("Error processing request for {0}", exception, req.RawUrl);

                if (!string.IsNullOrEmpty(exception.Message))
                {
                    var error = exception.Message.Replace(Environment.NewLine, " ");
                    error = RemoveControlCharacters(error);

                    res.AddHeader("X-Application-Error-Code", error);
                }
            }

            var hasHeaders = dto as IHasHeaders;

            if (hasHeaders != null)
            {
                if (!hasHeaders.Headers.ContainsKey("Server"))
                {
                    hasHeaders.Headers["Server"] = "Microsoft-NetCore/2.0, UPnP/1.0 DLNADOC/1.50";
                    //hasHeaders.Headers["Server"] = "Mono-HTTPAPI/1.1";
                }

                // Content length has to be explicitly set on on HttpListenerResponse or it won't be happy
                string contentLength;

                if (hasHeaders.Headers.TryGetValue("Content-Length", out contentLength) && !string.IsNullOrEmpty(contentLength))
                {
                    var length = long.Parse(contentLength, UsCulture);

                    if (length > 0)
                    {
                        res.SetContentLength(length);

                        //var listenerResponse = res.OriginalResponse as HttpListenerResponse;

                        //if (listenerResponse != null)
                        //{
                        //    // Disable chunked encoding. Technically this is only needed when using Content-Range, but
                        //    // anytime we know the content length there's no need for it
                        //    listenerResponse.SendChunked = false;
                        //    return;
                        //}

                        res.SendChunked = false;
                    }
                }
            }

            //res.KeepAlive = false;
        }

        /// <summary>
        /// Removes the control characters.
        /// </summary>
        /// <param name="inString">The in string.</param>
        /// <returns>System.String.</returns>
        private static string RemoveControlCharacters(string inString)
        {
            if (inString == null) return null;

            var newString = new StringBuilder();

            foreach (var ch in inString)
            {
                if (!char.IsControl(ch))
                {
                    newString.Append(ch);
                }
            }
            return newString.ToString();
        }

        public static async Task<object> CreateRequest(HttpListenerHost host, IRequest httpReq, RestPath restPath, ILogger logger)
        {
            var requestType = restPath.RequestType;

            if (RequireqRequestStream(requestType))
            {
                // Used by IRequiresRequestStream
                var requestParams = await GetRequestParams(httpReq).ConfigureAwait(false);
                var request = ServiceHandler.CreateRequest(httpReq, restPath, requestParams, host.CreateInstance(requestType));

                var rawReq = (IRequiresRequestStream)request;
                rawReq.RequestStream = httpReq.InputStream;
                return rawReq;
            }
            else
            {
                var requestParams = await GetFlattenedRequestParams(httpReq).ConfigureAwait(false);

                var requestDto = await CreateContentTypeRequest(host, httpReq, restPath.RequestType, httpReq.ContentType).ConfigureAwait(false);

                return CreateRequest(httpReq, restPath, requestParams, requestDto);
            }
        }

        public static bool RequireqRequestStream(Type requestType)
        {
            var requiresRequestStreamTypeInfo = typeof(IRequiresRequestStream).GetTypeInfo();

            return requiresRequestStreamTypeInfo.IsAssignableFrom(requestType.GetTypeInfo());
        }

        public static object CreateRequest(IRequest httpReq, RestPath restPath, Dictionary<string, string> requestParams, object requestDto)
        {
            string contentType;
            var pathInfo = !restPath.IsWildCardPath
                ? GetSanitizedPathInfo(httpReq.PathInfo, out contentType)
                : httpReq.PathInfo;

            return restPath.CreateRequest(pathInfo, requestParams, requestDto);
        }

        /// <summary>
        /// Duplicate Params are given a unique key by appending a #1 suffix
        /// </summary>
        private static async Task<Dictionary<string, string>> GetRequestParams(IRequest request)
        {
            var map = new Dictionary<string, string>();

            foreach (var name in request.QueryString.Keys)
            {
                if (name == null) continue; //thank you ASP.NET

                var values = request.QueryString.GetValues(name);
                if (values.Count == 1)
                {
                    map[name] = values[0];
                }
                else
                {
                    for (var i = 0; i < values.Count; i++)
                    {
                        map[name + (i == 0 ? "" : "#" + i)] = values[i];
                    }
                }
            }

            if ((IsMethod(request.Verb, "POST") || IsMethod(request.Verb, "PUT")))
            {
                var formData = await request.GetFormData().ConfigureAwait(false);
                if (formData != null)
                {
                    foreach (var name in formData.Keys)
                    {
                        if (name == null) continue; //thank you ASP.NET

                        var values = formData.GetValues(name);
                        if (values.Count == 1)
                        {
                            map[name] = values[0];
                        }
                        else
                        {
                            for (var i = 0; i < values.Count; i++)
                            {
                                map[name + (i == 0 ? "" : "#" + i)] = values[i];
                            }
                        }
                    }
                }
            }

            return map;
        }

        private static bool IsMethod(string method, string expected)
        {
            return string.Equals(method, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Duplicate params have their values joined together in a comma-delimited string
        /// </summary>
        private static async Task<Dictionary<string, string>> GetFlattenedRequestParams(IRequest request)
        {
            var map = new Dictionary<string, string>();

            foreach (var name in request.QueryString.Keys)
            {
                if (name == null) continue; //thank you ASP.NET
                map[name] = request.QueryString[name];
            }

            if ((IsMethod(request.Verb, "POST") || IsMethod(request.Verb, "PUT")))
            {
                var formData = await request.GetFormData().ConfigureAwait(false);
                if (formData != null)
                {
                    foreach (var name in formData.Keys)
                    {
                        if (name == null) continue; //thank you ASP.NET
                        map[name] = formData[name];
                    }
                }
            }

            return map;
        }

        private static void SetRoute(IRequest req, RestPath route)
        {
            req.Items["__route"] = route;
        }

        private static RestPath GetRoute(IRequest req)
        {
            object route;
            req.Items.TryGetValue("__route", out route);
            return route as RestPath;
        }
    }

}
