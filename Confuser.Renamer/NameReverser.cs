
namespace Confuser.Renamer
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Allows to reverse name encryption
    /// </summary>
    public class NameReverser : IDisposable
    {
        private readonly ICryptoTransform decryptor;

        public NameReverser(string encryptionPassword)
        {
            var algorithm = NameService.CreateReversibleAlgorithm(encryptionPassword);
            decryptor = algorithm.CreateDecryptor();
        }

        public void Dispose()
        {
            decryptor.Dispose();
        }

        /// <summary>
        /// Decodes the specified string (which may contain several encoded names).
        /// </summary>
        /// <param name="s">The string do decode.</param>
        /// <returns></returns>
        public string Decode(string s) {
            // split around start marker
            var parts = s.Split(new[] {NameService.ReversibleNameStartTag}, StringSplitOptions.None);
            // first part is always raw
            var decoded=new StringBuilder(parts[0]);
            // then process remaining parts
            foreach (var part in parts.Skip(1)) {
                // then split around end marker
                var encodedAndRaw = part.Split(new[] {NameService.ReversibleNameEndTag}, 2, StringSplitOptions.None);
                if (encodedAndRaw.Length == 1)
                    decoded.Append(encodedAndRaw[0]);
                else {
                    var base64String = new StringBuilder(encodedAndRaw[0]);
                    // some stack traces add a backslash
                    base64String = base64String.Replace(@"\", "");
                    while (base64String.Length%4 != 0)
                        base64String.Append('=');
                    // now, since it may actually be anything, try to convert, decrypt, etc. Any failure and we're inserting the original text
                    try {
                        var encryptedBytes = Convert.FromBase64String(base64String.ToString());
                        var nameBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                        var name = Encoding.UTF8.GetString(nameBytes, NameService.DummyHeaderLength, nameBytes.Length - NameService.DummyHeaderLength);
                        decoded.Append(name);
                    }
                    catch {
                        decoded.Append(NameService.ReversibleNameStartTag);
                        decoded.Append(encodedAndRaw[0]);
                        decoded.Append(NameService.ReversibleNameEndTag);
                    }
                    decoded.Append(encodedAndRaw[1]);
                }
            }
            return decoded.ToString();
        }

        /// <summary>
        /// Decodes the specified string (which may contain several encoded names).
        /// </summary>
        /// <param name="s">The string to decode.</param>
        /// <param name="encryptionPassword">The encryption password.</param>
        /// <returns></returns>
        public static string Decode(string s, string encryptionPassword) {
            using (var nameReverser = new NameReverser(encryptionPassword))
                return nameReverser.Decode(s);
        }
    }
}
