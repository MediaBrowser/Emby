using MediaBrowser.Common.Net;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MediaBrowser.Dlna.PlayTo
{
    public class SsdpHttpClient
    {
        //private const string USERAGENT = "Microsoft-Windows/6.2 UPnP/1.0 Microsoft-DLNA DLNADOC/1.50";
        //private const string FriendlyName = "MediaBrowser";

        //private static readonly CookieContainer Container = new CookieContainer();

        //private readonly IHttpClient _httpClient;

        //public SsdpHttpClient(IHttpClient httpClient)
        //{
        //    _httpClient = httpClient;
        //}

        //public async Task<XDocument> SendCommandAsync(string baseUrl, uService service, string command, string postData, string header = null)
        //{
        //    var serviceUrl = service.ControlURL;
        //    if (!serviceUrl.StartsWith("/"))
        //        serviceUrl = "/" + serviceUrl;

        //    var response = await PostSoapDataAsync(new Uri(baseUrl + serviceUrl), "\"" + service.ServiceType + "#" + command + "\"", postData, header)
        //        .ConfigureAwait(false);

        //    using (var stream = response.Content)
        //    {
        //        using (var reader = new StreamReader(stream, Encoding.UTF8))
        //        {
        //            return XDocument.Parse(reader.ReadToEnd(), LoadOptions.PreserveWhitespace);
        //        }
        //    }
        //}

        //public async Task SubscribeAsync(Uri url, string ip, int port, string localIp, int eventport, int timeOut = 3600)
        //{
        //    var options = new HttpRequestOptions
        //    {
        //        Url = url.ToString()
        //    };

        //    options.RequestHeaders["UserAgent"] = USERAGENT;
        //    options.RequestHeaders["HOST"] = ip + ":" + port;
        //    options.RequestHeaders["CALLBACK"] = "<" + localIp + ":" + eventport + ">";
        //    options.RequestHeaders["NT"] = "upnp:event";
        //    options.RequestHeaders["TIMEOUT"] = "Second - " + timeOut;
        //    //request.CookieContainer = Container;

        //    using (await _httpClient.Get(options).ConfigureAwait(false))
        //    {
        //    }
        //}

        //public async Task RespondAsync(Uri url, string ip, int port, string localIp, int eventport, int timeOut = 20000)
        //{
        //    var options = new HttpRequestOptions
        //    {
        //        Url = url.ToString()
        //    };

        //    options.RequestHeaders["UserAgent"] = USERAGENT;
        //    options.RequestHeaders["HOST"] = ip + ":" + port;
        //    options.RequestHeaders["CALLBACK"] = "<" + localIp + ":" + eventport + ">";
        //    options.RequestHeaders["NT"] = "upnp:event";
        //    options.RequestHeaders["TIMEOUT"] = "Second - 3600";
        //    //request.CookieContainer = Container;

        //    using (await _httpClient.Get(options).ConfigureAwait(false))
        //    {
        //    }
        //}

        //public async Task<XDocument> GetDataAsync(Uri url)
        //{
        //    var options = new HttpRequestOptions
        //    {
        //        Url = url.ToString()
        //    };

        //    options.RequestHeaders["UserAgent"] = USERAGENT;
        //    options.RequestHeaders["FriendlyName.DLNA.ORG"] = FriendlyName;
        //    //request.CookieContainer = Container;

        //    using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
        //    {
        //        using (var reader = new StreamReader(stream, Encoding.UTF8))
        //        {
        //            return XDocument.Parse(reader.ReadToEnd(), LoadOptions.PreserveWhitespace);
        //        }
        //    }
        //}

        //public Task<HttpResponseInfo> PostSoapDataAsync(Uri url, string soapAction, string postData, string header = null, int timeOut = 20000)
        //{
        //    if (!soapAction.StartsWith("\""))
        //        soapAction = "\"" + soapAction + "\"";

        //    var options = new HttpRequestOptions
        //    {
        //        Url = url.ToString()
        //    };

        //    options.RequestHeaders["SOAPAction"] = soapAction;
        //    options.RequestHeaders["Pragma"] = "no-cache";
        //    options.RequestHeaders["UserAgent"] = USERAGENT;
        //    options.RequestHeaders["FriendlyName.DLNA.ORG"] = FriendlyName;

        //    if (!string.IsNullOrWhiteSpace(header))
        //    {
        //        options.RequestHeaders["contentFeatures.dlna.org"] = header;
        //    }

        //    options.RequestContentType = "text/xml; charset=\"utf-8\"";
        //    options.RequestContent = postData;

        //    return _httpClient.Post(options);
        //}

        private const string USERAGENT = "Microsoft-Windows/6.2 UPnP/1.0 Microsoft-DLNA DLNADOC/1.50";
        private const string FRIENDLY_NAME = "MediaBrowser";
        private static CookieContainer container = new CookieContainer();

        internal static async Task<XDocument> SendCommandAsync(string baseUrl, uService service, string command, string postData, string header = null)
        {
            SsdpHttpClient httpClient = new SsdpHttpClient();
            string serviceUrl = service.ControlURL; ;
            if (!serviceUrl.StartsWith("/"))
                serviceUrl = "/" + serviceUrl;
            var response = await httpClient.PostSoapDataAsync(new Uri(baseUrl + serviceUrl), "\"" + service.ServiceType + "#" + command + "\"", postData, header);

            if (response == null || response.Stream == null)
                return null;

            return httpClient.ParseStream(response.Stream);
        }

        internal XDocument ParseStream(Stream stream)
        {
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            try
            {
                XDocument doc = XDocument.Parse(reader.ReadToEnd(), LoadOptions.PreserveWhitespace);
                stream.Dispose();
                return doc;
            }
            catch
            {
            }
            return null;
        }

        internal static string CreateDidlMeta(string value)
        {
            if (value == null)
                return string.Empty;
            string escapedData = value.Replace("<", "&lt;").Replace(">", "&gt;");

            return string.Format(BaseDidl, escapedData.Replace("\r\n", ""));
        }

        private const string BaseDidl = "&lt;DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns:dlna=\"urn:schemas-dlna-org:metadata-1-0/\"&gt;{0}&lt;/DIDL-Lite&gt;";


        internal async Task<bool> SubscribeAsync(Uri url, string ip, int port, string localIp, int eventport, int timeOut = 3600)
        {
            string response = string.Empty;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = container;
            request.Headers["UserAgent"] = USERAGENT;
            request.Headers["HOST"] = ip + ":" + port;
            request.Headers["CALLBACK"] = "<" + localIp + ":" + eventport + ">";
            request.Headers["NT"] = "upnp:event";
            request.Headers["TIMEOUT"] = "Second - " + timeOut;

            try
            {
                var rm = await GetResponseAsync(request);
                if (rm.StatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine("Subscription OK");
                    return true;
                }
                else
                {
                    Console.WriteLine("Subscription Failed. Error: {0}", rm.StatusDescription);
                }
            }
            catch
            {
            }
            return false;
        }

        internal async Task<bool> RespondAsync(Uri url, string ip, int port, string localIp, int eventport, int timeOut = 20000)
        {
            string response = string.Empty;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = container;
            request.Headers["UserAgent"] = USERAGENT;
            request.Headers["HOST"] = ip + ":" + port;
            request.Headers["CALLBACK"] = "<" + localIp + ":" + eventport + ">";
            request.Headers["NT"] = "upnp:event";
            request.Headers["TIMEOUT"] = "Second - 3600";

            try
            {
                var rm = await GetResponseAsync(request);
                if (rm.StatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine("Subscription OK");
                    return true;
                }
                else
                {
                    Console.WriteLine("Subscription Failed. Error: {0}", rm.StatusDescription);
                }
            }
            catch
            {
            }
            return false;
        }

        internal async Task<Stream> GetDataAsync(Uri url, int timeOut = 20000)
        {
            string response = string.Empty;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = container;
            request.Headers["UserAgent"] = USERAGENT;
            request.Headers["FriendlyName.DLNA.ORG"] = FRIENDLY_NAME;

            try
            {
                var rm = await GetResponseAsync(request);

                if (rm != null)
                {
                    var stream = rm.GetResponseStream();
                    return stream;
                }


            }
            catch
            {
            }

            return null;
        }

        internal async Task<uSoapResponse> PostSoapDataAsync(Uri url, string soapAction, string postData, string header = null, int timeOut = 20000)
        {
            string response = string.Empty;

            if (!soapAction.StartsWith("\""))
                soapAction = "\"" + soapAction + "\"";
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers["SOAPAction"] = soapAction;
            request.Headers["Pragma"] = "no-cache";
            request.Headers["UserAgent"] = USERAGENT;
            request.Headers["FriendlyName.DLNA.ORG"] = FRIENDLY_NAME;

            request.ContentType = "text/xml; charset=\"utf-8\"";
            request.Method = "POST";

            if (header != null)
            {
                request.Headers["contentFeatures.dlna.org"] = header;
            }

            try
            {
                using (var rq = await GetRequestStreamAsync(request))
                {
                    byte[] postArray = Encoding.UTF8.GetBytes(postData);
                    rq.Write(postArray, 0, postArray.Length);
                    rq.Dispose();
                }
                var rm = await GetResponseAsync(request);

                if (rm.StatusCode == HttpStatusCode.OK)
                {

                    var stream = rm.GetResponseStream();
                    return new uSoapResponse { StatusCode = rm.StatusCode.ToString(), Stream = stream };
                }
                else
                {
                    return new uSoapResponse { StatusCode = rm.StatusCode.ToString() };
                }


            }
            catch (Exception ex)
            {
                return new uSoapResponse { StatusCode = ex.Message };
            }
        }

        private Task<Stream> GetRequestStreamAsync(HttpWebRequest request)
        {
            var tcs = new TaskCompletionSource<Stream>();

            try
            {
                request.BeginGetRequestStream(iar =>
                {
                    try
                    {
                        var response = request.EndGetRequestStream(iar);
                        tcs.SetResult(response);
                    }
                    catch (Exception exc)
                    {
                        tcs.SetException(exc);
                    }
                }, null);
            }
            catch (Exception exc)
            {
                tcs.SetException(exc);
            }

            return tcs.Task;
        }

        private Task<HttpWebResponse> GetResponseAsync(HttpWebRequest request)
        {
            var tcs = new TaskCompletionSource<HttpWebResponse>();

            try
            {
                request.BeginGetResponse(iar =>
                {
                    try
                    {
                        var response = (HttpWebResponse)request.EndGetResponse(iar);
                        tcs.SetResult(response);
                    }
                    catch (Exception exc)
                    {
                        tcs.SetException(exc);
                    }
                }, null);
            }
            catch (Exception exc)
            {
                tcs.SetException(exc);
            }

            return tcs.Task;
        }
    }

    internal class uSoapResponse
    {
        internal string StatusCode
        { get; set; }

        internal Stream Stream
        { get; set; }
    }
}
