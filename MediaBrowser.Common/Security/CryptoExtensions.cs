using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Common.Security
{
    public static class CryptoExtensions
    {
        public static string GetSha1Hash(this string str, string salt = null)
        {
            salt = salt ?? String.Empty;
            using (var provider = SHA1.Create())
            {
                var hash = provider.ComputeHash(Encoding.UTF8.GetBytes(str + salt));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        public static string Encrypt(this RSACryptoServiceProvider key, string data)
        {
            try
            {
                byte[] decrypted = Encoding.UTF8.GetBytes(data);
                byte[] encrypted = key.Encrypt(decrypted, false);
                return Convert.ToBase64String(encrypted);
            }
            catch { return null; }
        }

        public static string Decrypt(this RSACryptoServiceProvider key, string encryptedData)
        {
            try
            {
                byte[] encrypted = Convert.FromBase64String(encryptedData.Trim());
                byte[] decrypted = key.Decrypt(encrypted, false);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch { return null; }
        }

        public static string Encrypt(this AesCryptoServiceProvider aes, string data)
        {
            try
            {
                byte[] decrypted = Encoding.UTF8.GetBytes(data);
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] encrypted = encryptor.TransformFinalBlock(decrypted, 0, decrypted.Length);
                    return Convert.ToBase64String(encrypted);
                }
            }
            catch
            {
                return null;
            }
        }

        public static string Decrypt(this AesCryptoServiceProvider aes, string encryptedData)
        {
            try
            {
                byte[] encrypted = Convert.FromBase64String(encryptedData);
                using (ICryptoTransform decrypt = aes.CreateDecryptor())
                {
                    byte[] decrypted = decrypt.TransformFinalBlock(encrypted, 0, encrypted.Length);
                    return Encoding.UTF8.GetString(decrypted);
                }
            }
            catch
            {
                return null;
            }
        }

        //http://stackoverflow.com/questions/23734792/c-sharp-export-private-public-rsa-key-from-rsacryptoserviceprovider-to-pem-strin/25591659#25591659
        public static string ExportPublicKey(this RSA csp)
        {
            StringBuilder builder = new StringBuilder();
            var parameters = csp.ExportParameters(false);
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write((byte)0x30); // SEQUENCE
                using (var innerStream = new MemoryStream())
                {
                    var innerWriter = new BinaryWriter(innerStream);
                    innerWriter.Write((byte)0x30); // SEQUENCE
                    EncodeLength(innerWriter, 13);
                    innerWriter.Write((byte)0x06); // OBJECT IDENTIFIER
                    var rsaEncryptionOid = new byte[] { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01 };
                    EncodeLength(innerWriter, rsaEncryptionOid.Length);
                    innerWriter.Write(rsaEncryptionOid);
                    innerWriter.Write((byte)0x05); // NULL
                    EncodeLength(innerWriter, 0);
                    innerWriter.Write((byte)0x03); // BIT STRING
                    using (var bitStringStream = new MemoryStream())
                    {
                        var bitStringWriter = new BinaryWriter(bitStringStream);
                        bitStringWriter.Write((byte)0x00); // # of unused bits
                        bitStringWriter.Write((byte)0x30); // SEQUENCE
                        using (var paramsStream = new MemoryStream())
                        {
                            var paramsWriter = new BinaryWriter(paramsStream);
                            EncodeIntegerBigEndian(paramsWriter, parameters.Modulus); // Modulus
                            EncodeIntegerBigEndian(paramsWriter, parameters.Exponent); // Exponent
                            var paramsLength = (int)paramsStream.Length;
                            EncodeLength(bitStringWriter, paramsLength);
                            bitStringWriter.Write(paramsStream.GetBuffer(), 0, paramsLength);
                        }
                        var bitStringLength = (int)bitStringStream.Length;
                        EncodeLength(innerWriter, bitStringLength);
                        innerWriter.Write(bitStringStream.GetBuffer(), 0, bitStringLength);
                    }
                    var length = (int)innerStream.Length;
                    EncodeLength(writer, length);
                    writer.Write(innerStream.GetBuffer(), 0, length);
                }
                builder.AppendLine("-----BEGIN PUBLIC KEY-----");
                builder.AppendLine(Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length));
                builder.AppendLine("-----END PUBLIC KEY-----");
            }
            return builder.ToString();
        }
        private static void EncodeIntegerBigEndian(BinaryWriter stream, byte[] value, bool forceUnsigned = true)
        {
            stream.Write((byte)0x02); // INTEGER
            var prefixZeros = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != 0) break;
                prefixZeros++;
            }
            if (value.Length - prefixZeros == 0)
            {
                EncodeLength(stream, 1);
                stream.Write((byte)0);
            }
            else
            {
                if (forceUnsigned && value[prefixZeros] > 0x7f)
                {
                    // Add a prefix zero to force unsigned if the MSB is 1
                    EncodeLength(stream, value.Length - prefixZeros + 1);
                    stream.Write((byte)0);
                }
                else
                {
                    EncodeLength(stream, value.Length - prefixZeros);
                }
                for (var i = prefixZeros; i < value.Length; i++)
                {
                    stream.Write(value[i]);
                }
            }
        }

        private static void EncodeLength(BinaryWriter stream, int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException("length", "Length must be non-negative");
            if (length < 0x80)
            {
                // Short form
                stream.Write((byte)length);
            }
            else
            {
                // Long form
                var temp = length;
                var bytesRequired = 0;
                while (temp > 0)
                {
                    temp >>= 8;
                    bytesRequired++;
                }
                stream.Write((byte)(bytesRequired | 0x80));
                for (var i = bytesRequired - 1; i >= 0; i--)
                {
                    stream.Write((byte)(length >> (8 * i) & 0xff));
                }
            }
        }
    }
}