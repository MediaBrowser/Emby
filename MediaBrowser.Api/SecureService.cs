using MediaBrowser.Common.Security;
using MediaBrowser.Controller.Net;
using ServiceStack;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Api
{    
    [Route("/Secure")]
    public class SecureInfo { }
    [Route("/Secure/{Route*}")]
    public class SecureMsg
    {
        public string Ciphertext { get; set; }
        public string IV { get; set; }
        public string Key { get; set; }
        public string Route { get; set; }

        public string Decrypt(RSACryptoServiceProvider rsaKey)
        {
            using (var aes = new AesCryptoServiceProvider()) {
                aes.BlockSize = 128;
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = Convert.FromBase64String(rsaKey.Decrypt(Key));
                aes.IV = Convert.FromBase64String(IV);
                return aes.Decrypt(Ciphertext);
            }
        }
    }
    public class Msg
    {
        public DateTime ValidUntil { get; set; }
        public string Data { get; set; }
    }
    public class SecureService : BaseApiService
    {
        private readonly IHttpServer _host;
        private static readonly RSACryptoServiceProvider _key = new RSACryptoServiceProvider(2048);

        public SecureService(IHttpServer host)
        {
            _host = host;
        }
        public string Any(SecureInfo info)
        {
            return _key.ExportPublicKey();
        }
        public object Any(SecureMsg request)
        {
            var msg = request.Decrypt(_key).FromJson<Msg>();
            if (msg.ValidUntil < DateTime.UtcNow) { throw new SecurityException("Invalid Msg"); }
            return _host.InternalRouteRequest(request.Route, base.Request.Verb, base.Request, msg.Data);
        }        
    }
}
