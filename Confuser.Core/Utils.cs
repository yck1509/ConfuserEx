using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using System.IO;
using System.Security.Cryptography;

namespace Confuser.Core
{
    /// <summary>
    /// Provides a set of utility methods
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Gets the value associated with the specified key, or default value if the key does not exists.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="defValue">The default value.</param>
        /// <returns>The value associated with the specified key, or the default value if the key does not exists</returns>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defValue = default(TValue))
        {
            TValue ret;
            if (dictionary.TryGetValue(key, out ret))
                return ret;
            else
                return defValue;
        }

        /// <summary>
        /// Gets the value associated with the specified key, or default value if the key does not exists.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="defValueFactory">The default value factory function.</param>
        /// <returns>The value associated with the specified key, or the default value if the key does not exists</returns>
        public static TValue GetValueOrDefaultLazy<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            Func<TKey, TValue> defValueFactory)
        {
            TValue ret;
            if (dictionary.TryGetValue(key, out ret))
                return ret;
            else
                return defValueFactory(key);
        }

        /// <summary>
        /// Adds the specified key and value to the multi dictionary.
        /// </summary>
        /// <typeparam name="TKey">The type of key.</typeparam>
        /// <typeparam name="TValue">The type of value.</typeparam>
        /// <param name="self">The dictionary to add to.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="System.ArgumentNullException">key is <c>null</c>.</exception>
        public static void AddListEntry<TKey, TValue>(this Dictionary<TKey, List<TValue>> self, TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            List<TValue> list;
            if (!self.TryGetValue(key, out list))
                list = self[key] = new List<TValue>();
            list.Add(value);
        }

        /// <summary>
        /// OBtains the relative path from the specified base path.
        /// </summary>
        /// <param name="filespec">The file path.</param>
        /// <param name="folder">The base path.</param>
        /// <returns>The path of <paramref name="filespec"/> relative to <paramref name="folder"/>.</returns>
        public static string GetRelativePath(string filespec, string folder)
        {
            //http://stackoverflow.com/a/703292/462805

            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// If the input string is empty, return null; otherwise, return the original input string.
        /// </summary>
        /// <param name="val">The input string.</param>
        /// <returns><c>null</c> if the input string is empty; otherwise, the original input string.</returns>
        public static string NullIfEmpty(this string val)
        {
            if (string.IsNullOrEmpty(val))
                return null;
            return val;
        }

        /// <summary>
        /// Compute the SHA1 hash of the input buffer.
        /// </summary>
        /// <param name="buffer">The input buffer.</param>
        /// <returns>The SHA1 hash of the input buffer.</returns>
        public static byte[] SHA1(byte[] buffer)
        {
            SHA1Managed sha = new SHA1Managed();
            return sha.ComputeHash(buffer);
        }

        /// <summary>
        /// Xor the values in the two buffer together.
        /// </summary>
        /// <param name="buffer1">The input buffer 1.</param>
        /// <param name="buffer2">The input buffer 2.</param>
        /// <returns>The result buffer.</returns>
        /// <exception cref="System.ArgumentException">Length of the two buffers are not equal.</exception>
        public static byte[] Xor(byte[] buffer1, byte[] buffer2)
        {
            if (buffer1.Length != buffer2.Length)
                throw new ArgumentException("Length mismatched.");
            byte[] ret = new byte[buffer1.Length];
            for (int i = 0; i < ret.Length; i++)
                ret[i] = (byte)(buffer1[i] ^ buffer2[i]);
            return ret;
        }

        /// <summary>
        /// Compute the SHA256 hash of the input buffer.
        /// </summary>
        /// <param name="buffer">The input buffer.</param>
        /// <returns>The SHA256 hash of the input buffer.</returns>
        public static byte[] SHA256(byte[] buffer)
        {
            SHA256Managed sha = new SHA256Managed();
            return sha.ComputeHash(buffer);
        }

        /// <summary>
        /// Encoding the buffer to a string using specified charset.
        /// </summary>
        /// <param name="buff">The input buffer.</param>
        /// <param name="charset">The charset.</param>
        /// <returns>The encoded string.</returns>
        public static string EncodeString(byte[] buff,char[] charset)
        {
            int current = buff[0];
            StringBuilder ret = new StringBuilder();
            for (int i = 1; i < buff.Length; i++)
            {
                current = (current << 8) + buff[i];
                while (current >= charset.Length)
                {
                    ret.Append(charset[current % charset.Length]);
                    current /= charset.Length;
                }
            }
            return ret.ToString();
        }


        static char[] hexCharset = "0123456789abcdef".ToCharArray();
        /// <summary>
        /// Encode the buffer to a hexadecimal string.
        /// </summary>
        /// <param name="buff">The input buffer.</param>
        /// <returns>A hexadecimal representation of input buffer.</returns>
        public static string ToHexString(byte[] buff)
        {
            char[] ret = new char[buff.Length * 2];
            int i = 0;
            foreach (var val in buff)
            {
                ret[i++] = hexCharset[val >> 4];
                ret[i++] = hexCharset[val & 0xf];
            }
            return new string(ret);
        }

        /// <summary>
        /// Removes all elements that match the conditions defined by the specified predicate from a the list.
        /// </summary>
        /// <typeparam name="T">The type of the elements of <paramref name="self" />.</typeparam>
        /// <param name="self">The list to remove from.</param>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <returns><paramref name="self" /> for method chaining.</returns>
        public static IList<T> RemoveWhere<T>(this IList<T> self, Predicate<T> match)
        {
            for (int i = self.Count - 1; i >= 0; i--)
            {
                if (match(self[i]))
                    self.RemoveAt(i);
            }
            return self;
        }
    }

    /// <summary>
    /// Represents a 2-tuple, or pair.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    public class Tuple<T1, T2>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Tuple{T1, T2}"/> class.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        public Tuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        /// <summary>
        /// Gets the value of the first component.
        /// </summary>
        /// <value>The value of the first component.</value>
        public T1 Item1 { get; private set; }

        /// <summary>
        /// Gets the value of the second component.
        /// </summary>
        /// <value>The value of the second component.</value>
        public T2 Item2 { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            Tuple<T1, T2> other = obj as Tuple<T1, T2>;
            if (other == null) return false;
            return object.Equals(Item1, other.Item1) && object.Equals(Item2, other.Item2);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hash1 = EqualityComparer<T1>.Default.GetHashCode(Item1);
            int hash2 = EqualityComparer<T1>.Default.GetHashCode(Item1);
            return ((hash1 << 5) + hash1) ^ hash2;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("({0}, {1})", Item1, Item2);
        }
    }

    /// <summary>
    /// Provides static methods for creating tuple objects.
    /// </summary>
    public static class Tuple
    {
        /// <summary>
        /// Creates a new 2-tuple, or pair.
        /// </summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <param name="item2">The value of the second component of the tuple.</param>
        /// <returns>A 2-tuple whose value is (item1, item2).</returns>
        public static Tuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
        {
            return new Tuple<T1, T2>(item1, item2);
        }
    }
}
