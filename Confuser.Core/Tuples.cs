using System.Collections.Generic;

namespace Confuser.Core {
	/// <summary>
	///     Represents a 2-tuple, or pair.
	/// </summary>
	/// <typeparam name="T1">The type of the tuple's first component.</typeparam>
	/// <typeparam name="T2">The type of the tuple's second component.</typeparam>
	public class Tuple<T1, T2> {
		/// <summary>
		///     Initializes a new instance of the <see cref="Tuple{T1, T2}" /> class.
		/// </summary>
		/// <param name="item1">The value of the tuple's first component.</param>
		/// <param name="item2">The value of the tuple's second component.</param>
		public Tuple(T1 item1, T2 item2) {
			Item1 = item1;
			Item2 = item2;
		}

		/// <summary>
		///     Gets or sets the value of the first component.
		/// </summary>
		/// <value>The value of the first component.</value>
		public T1 Item1 { get; set; }

		/// <summary>
		///     Gets or sets the value of the second component.
		/// </summary>
		/// <value>The value of the second component.</value>
		public T2 Item2 { get; set; }

		/// <inheritdoc />
		public override bool Equals(object obj) {
			var other = obj as Tuple<T1, T2>;
			if (other == null) return false;
			return Equals(Item1, other.Item1) && Equals(Item2, other.Item2);
		}

		/// <inheritdoc />
		public override int GetHashCode() {
			int hash1 = EqualityComparer<T1>.Default.GetHashCode(Item1);
			int hash2 = EqualityComparer<T2>.Default.GetHashCode(Item2);
			return ((hash1 << 5) + hash1) ^ hash2;
		}

		/// <inheritdoc />
		public override string ToString() {
			return string.Format("({0}, {1})", Item1, Item2);
		}
	}

	/// <summary>
	///     Represents a 3-tuple, or triple.
	/// </summary>
	/// <typeparam name="T1">The type of the tuple's first component.</typeparam>
	/// <typeparam name="T2">The type of the tuple's second component.</typeparam>
	/// <typeparam name="T3">The type of the tuple's third component.</typeparam>
	public class Tuple<T1, T2, T3> {
		/// <summary>
		///     Initializes a new instance of the <see cref="Tuple{T1, T2, T3}" /> class.
		/// </summary>
		/// <param name="item1">The value of the tuple's first component.</param>
		/// <param name="item2">The value of the tuple's second component.</param>
		/// <param name="item3">The value of the tuple's third component.</param>
		public Tuple(T1 item1, T2 item2, T3 item3) {
			Item1 = item1;
			Item2 = item2;
			Item3 = item3;
		}

		/// <summary>
		///     Gets or sets the value of the first component.
		/// </summary>
		/// <value>The value of the first component.</value>
		public T1 Item1 { get; set; }

		/// <summary>
		///     Gets or sets the value of the second component.
		/// </summary>
		/// <value>The value of the second component.</value>
		public T2 Item2 { get; set; }

		/// <summary>
		///     Gets or sets the value of the third component.
		/// </summary>
		/// <value>The value of the third component.</value>
		public T3 Item3 { get; set; }

		/// <inheritdoc />
		public override bool Equals(object obj) {
			var other = obj as Tuple<T1, T2, T3>;
			if (other == null) return false;
			return Equals(Item1, other.Item1) && Equals(Item2, other.Item2) && Equals(Item3, other.Item3);
		}

		/// <inheritdoc />
		public override int GetHashCode() {
			int hash1 = EqualityComparer<T1>.Default.GetHashCode(Item1);
			int hash2 = EqualityComparer<T2>.Default.GetHashCode(Item2);
			int hash3 = EqualityComparer<T3>.Default.GetHashCode(Item3);
			int th = ((hash1 << 5) + hash1) ^ hash2;
			return ((th << 5) + th) ^ hash3;
		}

		/// <inheritdoc />
		public override string ToString() {
			return string.Format("({0}, {1}, {2})", Item1, Item2, Item3);
		}
	}

	/// <summary>
	///     Provides static methods for creating tuple objects.
	/// </summary>
	public static class Tuple {
		/// <summary>
		///     Creates a new 2-tuple, or pair.
		/// </summary>
		/// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
		/// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
		/// <param name="item1">The value of the first component of the tuple.</param>
		/// <param name="item2">The value of the second component of the tuple.</param>
		/// <returns>A 2-tuple whose value is (item1, item2).</returns>
		public static Tuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2) {
			return new Tuple<T1, T2>(item1, item2);
		}

		/// <summary>
		///     Creates a new 3-tuple, or triple.
		/// </summary>
		/// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
		/// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
		/// <typeparam name="T3">The type of the third component of the tuple.</typeparam>
		/// <param name="item1">The value of the first component of the tuple.</param>
		/// <param name="item2">The value of the second component of the tuple.</param>
		/// <param name="item3">The value of the third component of the tuple.</param>
		/// <returns>A 3-tuple whose value is (item1, item2, item3).</returns>
		public static Tuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3) {
			return new Tuple<T1, T2, T3>(item1, item2, item3);
		}
	}
}