using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;

namespace Confuser.Core
{
    /// <summary>
    /// Provides methods to annotate objects.
    /// </summary>
    /// <remarks>
    /// The annotations are stored using <see cref="WeakReference"/>
    /// </remarks>
    public class Annotations
    {
        /// <summary>
        /// Represent a key using <see cref="WeakReference"/>.
        /// </summary>
        class WeakReferenceKey : WeakReference
        {
            /// <inheritdoc/>
            public WeakReferenceKey(object target)
                : base(target)
            {
                HashCode = target.GetHashCode();
            }

            /// <summary>
            /// Gets the hash code of the target object.
            /// </summary>
            /// <value>The hash code.</value>
            public int HashCode { get; private set; }
        }

        /// <summary>
        /// Equality comparer of weak references.
        /// </summary>
        class WeakReferenceComparer : IEqualityComparer<object>
        {
            /// <summary>
            /// Prevents a default instance of the <see cref="WeakReferenceComparer"/> class from being created.
            /// </summary>
            private WeakReferenceComparer()
            {
            }

            /// <summary>
            /// The singleton instance of this comparer.
            /// </summary>
            public static readonly WeakReferenceComparer Instance = new WeakReferenceComparer();

            /// <inheritdoc/>
            public new bool Equals(object x, object y)
            {
                if (y is WeakReferenceKey && !(x is WeakReference))
                    return Equals(y, x);
                WeakReferenceKey xWeak = x as WeakReferenceKey;
                WeakReferenceKey yWeak = y as WeakReferenceKey;
                if (xWeak != null && yWeak != null)
                {
                    return xWeak.IsAlive && yWeak.IsAlive && object.ReferenceEquals(xWeak.Target, yWeak.Target);
                }
                else if (xWeak != null && yWeak == null)
                {
                    return xWeak.IsAlive && object.ReferenceEquals(xWeak.Target, y);
                }
                else if (xWeak == null && yWeak == null)
                {
                    return xWeak.IsAlive && object.ReferenceEquals(xWeak.Target, y);
                }
                throw new UnreachableException();
            }

            /// <inheritdoc/>
            public int GetHashCode(object obj)
            {
                if (obj is WeakReferenceKey)
                    return ((WeakReferenceKey)obj).HashCode;
                else
                    return obj.GetHashCode();
            }
        }

        readonly Dictionary<object, ListDictionary> annotations = new Dictionary<object, ListDictionary>(WeakReferenceComparer.Instance);

        /// <summary>
        /// Retrieves the annotation on the specified object associated with the specified key.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key of annotation.</param>
        /// <param name="defValue">The default value if the specified annotation does not exists on the object.</param>
        /// <returns>The value of annotation, or null if the annotation does not exist.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="obj"/> or <paramref name="key"/> is null.
        /// </exception>
        public TValue Get<TValue>(object obj, object key, TValue defValue = default(TValue))
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            if (key == null)
                throw new ArgumentNullException("key");

            ListDictionary objAnno;
            if (!annotations.TryGetValue(obj, out objAnno))
                return defValue;
            if (!objAnno.Contains(key))
                return defValue;
            return (TValue)Convert.ChangeType(objAnno[key], typeof(TValue));
        }

        /// <summary>
        /// Sets a annotation on the specified object.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key of annotation.</param>
        /// <param name="value">The value of annotation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="obj"/> or <paramref name="key"/> is null.
        /// </exception>
        public void Set<TValue>(object obj, object key, TValue value)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            if (key == null)
                throw new ArgumentNullException("key");

            ListDictionary objAnno;
            if (!annotations.TryGetValue(obj, out objAnno))
                objAnno = annotations[new WeakReferenceKey(obj)] = new ListDictionary();
            objAnno[key] = value;
        }

        /// <summary>
        /// Trims the annotations of unreachable objects from this instance.
        /// </summary>
        public void Trim()
        {
            foreach (var key in annotations.Where(kvp => !((WeakReferenceKey)kvp.Key).IsAlive).Select(kvp => kvp.Key))
                annotations.Remove(key);
        }
    }
}
