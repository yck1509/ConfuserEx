using System;

namespace Confuser.Core {
	/// <summary>
	///     Indicates the <see cref="Protection" /> must initialize before the specified protections.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class BeforeProtectionAttribute : Attribute {
		/// <summary>
		///     Initializes a new instance of the <see cref="BeforeProtectionAttribute" /> class.
		/// </summary>
		/// <param name="ids">The full IDs of the specified protections.</param>
		public BeforeProtectionAttribute(params string[] ids) {
			Ids = ids;
		}

		/// <summary>
		///     Gets the full IDs of the specified protections.
		/// </summary>
		/// <value>The IDs of protections.</value>
		public string[] Ids { get; private set; }
	}

	/// <summary>
	///     Indicates the <see cref="Protection" /> must initialize after the specified protections.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class AfterProtectionAttribute : Attribute {
		/// <summary>
		///     Initializes a new instance of the <see cref="BeforeProtectionAttribute" /> class.
		/// </summary>
		/// <param name="ids">The full IDs of the specified protections.</param>
		public AfterProtectionAttribute(params string[] ids) {
			Ids = ids;
		}

		/// <summary>
		///     Gets the full IDs of the specified protections.
		/// </summary>
		/// <value>The IDs of protections.</value>
		public string[] Ids { get; private set; }
	}
}