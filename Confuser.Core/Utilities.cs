using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using System.IO;

namespace Confuser.Core
{
    /// <summary>
    /// Utilities methods
    /// </summary>
    public static class Utilities
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
        public static TValue GetValueOrDefault<TKey, TValue>(
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
        /// Finds all definitions of interest in a module.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <returns>A collection of all required definitions</returns>
        public static IEnumerable<IDefinition> FindDefinitions(this ModuleDef module)
        {
            yield return module;
            foreach (var type in module.GetTypes())
            {
                yield return type;
                foreach (var method in type.Methods)
                    yield return method;
                foreach (var field in type.Fields)
                    yield return field;
                foreach (var prop in type.Properties)
                    yield return prop;
                foreach (var evt in type.Events)
                    yield return evt;
            }
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
