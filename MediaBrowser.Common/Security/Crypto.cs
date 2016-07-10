using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Common.Security
{
    public static class Crypto
    {
        public static string GetSha1(string str)
        {
            using (var provider = SHA1.Create())
            {
                var hash = provider.ComputeHash(Encoding.UTF8.GetBytes(str));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
        public static string Encrypt(AsymmetricAlgorithm key, string data)
        {
            var cipher = (RSACryptoServiceProvider)key;
            try
            {
                byte[] decrypted = Encoding.UTF8.GetBytes(data);
                byte[] encrypted = cipher.Encrypt(decrypted, false);
                return Convert.ToBase64String(encrypted);
            }
            catch { return null; }
        }

        public static string Decrypt(AsymmetricAlgorithm key, string encryptedData)
        {
            var cipher = (RSACryptoServiceProvider)key;
            try
            {
                byte[] encrypted = Convert.FromBase64String(encryptedData.Trim());
                byte[] decrypted = cipher.Decrypt(encrypted, false);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch { return null; }
        }
    }
}
