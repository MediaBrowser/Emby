using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Common.Net
{
    public static class CertUtilities
    {
        //SOURCE: http://pstaev.blogspot.com/2010/08/convert-rsa-public-key-from-xml-to-pem.html
        public static string ToPEM(this PublicKey key)
        {
            List<byte> arrBinaryPublicKey = new List<byte>();
            StringBuilder builder = new StringBuilder();
            byte[] oid =
            { 0x30, 0xD, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x1, 0x1, 0x5, 0x0 }; // Object ID for RSA

            //Transform the public key to PEM Base64 Format
            arrBinaryPublicKey = key.EncodedKeyValue.RawData.ToList();
            arrBinaryPublicKey.Insert(0, 0x0); // Add NULL value

            CalculateAndAppendLength(ref arrBinaryPublicKey);

            arrBinaryPublicKey.Insert(0, 0x3);
            arrBinaryPublicKey.InsertRange(0, oid);

            CalculateAndAppendLength(ref arrBinaryPublicKey);

            arrBinaryPublicKey.Insert(0, 0x30);
            //End Transformation

            builder.AppendLine("-----BEGIN RSA PUBLIC KEY-----");
            builder.AppendLine(Convert.ToBase64String(arrBinaryPublicKey.ToArray()));
            builder.AppendLine("-----END RSA PUBLIC KEY-----");
            return builder.ToString();

        }

        private static void CalculateAndAppendLength(ref List<byte> arrBinaryData)
        {
            int nLen;
            nLen = arrBinaryData.Count;
            if (nLen <= byte.MaxValue)
            {
                arrBinaryData.Insert(0, Convert.ToByte(nLen));
                arrBinaryData.Insert(0, 0x81); //This byte means that the length fits in one byte
            }
            else
            {
                arrBinaryData.Insert(0, Convert.ToByte(nLen % (byte.MaxValue + 1)));
                arrBinaryData.Insert(0, Convert.ToByte(nLen / (byte.MaxValue + 1)));
                arrBinaryData.Insert(0, 0x82); //This byte means that the length fits in two byte
            }

        }

        public static string GetPublicKeyFromCert(string path,string certPass = "")
        {
            X509Certificate2 cert = new X509Certificate2(path, certPass, X509KeyStorageFlags.Exportable);
            return cert.PublicKey.ToPEM();
        }
    }
}
