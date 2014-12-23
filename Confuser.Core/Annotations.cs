using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Confuser.Core {
	/// <summary>
	///     Provides methods to annotate objects.
	/// </summary>
	/// <remarks>
	///     The annotations are stored using <see cref="WeakReference" />
	/// </remarks>
	public class Annotations {
		readonly Dictionary<object, ListDictionary> annotations = new Dictionary<object, ListDictionary>(WeakReferenceComparer.Instance);

		/// <summary>
		///     Retrieves the annotation on the specified object associated with the specified key.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="obj">The object.</param>
		/// <param name="key">The key of annotation.</param>
		/// <param name="defValue">The default value if the specified annotation does not exists on the object.</param>
		/// <returns>The value of annotation, or default value if the annotation does not exist.</returns>
		/// <exception cref="System.ArgumentNullException">
		///     <paramref name="obj" /> or <paramref name="key" /> is <c>null</c>.
		/// </exception>
		public TValue Get<TValue>(object obj, object key, TValue defValue = default(TValue)) {
			if (obj == null)
				throw new ArgumentNullException("obj");
			if (key == null)
				throw new ArgumentNullException("key");

			ListDictionary objAnno;
			if (!annotations.TryGetValue(obj, out objAnno))
				return defValue;
			if (!objAnno.Contains(key))
				return defValue;

			Type valueType = typeof(TValue);
			if (valueType.IsValueType)
				return (TValue)Convert.ChangeType(objAnno[key], typeof(TValue));
			return (TValue)objAnno[key];
		}

		/// <summary>
		///     Retrieves the annotation on the specified object associated with the specified key.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="obj">The object.</param>
		/// <param name="key">The key of annotation.</param>
		/// <param name="defValueFactory">The default value factory function.</param>
		/// <returns>The value of annotation, or default value if the annotation does not exist.</returns>
		/// <exception cref="System.ArgumentNullException">
		///     <paramref name="obj" /> or <paramref name="key" /> is <c>null</c>.
		/// </exception>
		public TValue GetLazy<TValue>(object obj, object key, Func<object, TValue> defValueFactory) {
			if (obj == null)
				throw new ArgumentNullException("obj");
			if (key == null)
				throw new ArgumentNullException("key");

			ListDictionary objAnno;
			if (!annotations.TryGetValue(obj, out objAnno))
				return defValueFactory(key);
			if (!objAnno.Contains(key))
				return defValueFactory(key);

			Type valueType = typeof(TValue);
			if (valueType.IsValueType)
				return (TValue)Convert.ChangeType(objAnno[key], typeof(TValue));
			return (TValue)objAnno[key];
		}

		/// <summary>
		///     Retrieves or create the annotation on the specified object associated with the specified key.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="obj">The object.</param>
		/// <param name="key">The key of annotation.</param>
		/// <param name="factory">The factory function to create the annotation value when the annotation does not exist.</param>
		/// <returns>The value of annotation, or the newly created value.</returns>
		/// <exception cref="System.ArgumentNullException">
		///     <paramref name="obj" /> or <paramref name="key" /> is <c>null</c>.
		/// </exception>
		public TValue GetOrCreate<TValue>(object obj, object key, Func<object, TValue> factory) {
			if (obj == null)
				throw new ArgumentNullException("obj");
			if (key == null)
				throw new ArgumentNullException("key");

			ListDictionary objAnno;
			if (!annotations.TryGetValue(obj, out objAnno))
				objAnno = annotations[new WeakReferenceKey(obj)] = new ListDictionary();
			TValue ret;
			if (objAnno.Contains(key)) {
				Type valueType = typeof(TValue);
				if (valueType.IsValueType)
					return (TValue)Convert.ChangeType(objAnno[key], typeof(TValue));
				return (TValue)objAnno[key];
			}
			objAnno[key] = ret = factory(key);
			return ret;
		}

		/// <summary>
		///     Sets an annotation on the specified object.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="obj">The object.</param>
		/// <param name="key">The key of annotation.</param>
		/// <param name="value">The value of annotation.</param>
		/// <exception cref="System.ArgumentNullException">
		///     <paramref name="obj" /> or <paramref name="key" /> is <c>null</c>.
		/// </exception>
		public void Set<TValue>(object obj, object key, TValue value) {
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
		///     Trims the annotations of unreachable objects from this instance.
		/// </summary>
		public void Trim() {
			foreach (object key in annotations.Where(kvp => !((WeakReferenceKey)kvp.Key).IsAlive).Select(kvp => kvp.Key))
				annotations.Remove(key);
		}

		/// <summary>
		///     Equality comparer of weak references.
		/// </summary>
		class WeakReferenceComparer : IEqualityComparer<object> {
			/// <summary>
			///     The singleton instance of this comparer.
			/// </summary>
			public static readonly WeakReferenceComparer Instance = new WeakReferenceComparer();

			/// <summary>
			///     Prevents a default instance of the <see cref="WeakReferenceComparer" /> class from being created.
			/// </summary>
			WeakReferenceComparer() { }

			/// <inheritdoc />
			public new bool Equals(object x, object y) {
				if (y is WeakReferenceKey && !(x is WeakReference))
					return Equals(y, x);
				var xWeak = x as WeakReferenceKey;
				var yWeak = y as WeakReferenceKey;
				if (xWeak != null && yWeak != null) {
					return xWeak.IsAlive && yWeak.IsAlive && ReferenceEquals(xWeak.Target, yWeak.Target);
				}
				if (xWeak != null && yWeak == null) {
					return xWeak.IsAlive && ReferenceEquals(xWeak.Target, y);
				}
				if (xWeak == null && yWeak == null) {
					return xWeak.IsAlive && ReferenceEquals(xWeak.Target, y);
				}
				throw new UnreachableException();
			}

			/// <inheritdoc />
			public int GetHashCode(object obj) {
				if (obj is WeakReferenceKey)
					return ((WeakReferenceKey)obj).HashCode;
				return obj.GetHashCode();
			}
		}

		/// <summary>
		///     Represent a key using <see cref="WeakReference" />.
		/// </summary>
		class WeakReferenceKey : WeakReference {
			/// <inheritdoc />
			public WeakReferenceKey(object target)
				: base(target) {
				HashCode = target.GetHashCode();
			}

			/// <summary>
			///     Gets the hash code of the target object.
			/// </summary>
			/// <value>The hash code.</value>
			public int HashCode { get; private set; }
		}
	}
}