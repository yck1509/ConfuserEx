using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Core.API {
	/// <summary>
	///     A data store.
	/// </summary>
	public interface IDataStore {
		/// <summary>
		///     Gets the priority of this data store; higher priority means it
		///     would be tried earlier.
		/// </summary>
		/// <value>The priority of this data store.</value>
		int Priority { get; }

		/// <summary>
		///     Gets the number of keys this predicate has.
		/// </summary>
		/// <remarks>
		///     Keys are used by the data store to encrypt data/whatever purpose.
		/// </remarks>
		/// <value>The number of keys this data store has.</value>
		int KeyCount { get; }

		/// <summary>
		///     Determines whether this data store can be used in the specified method.
		/// </summary>
		/// <param name="method">The method.</param>
		/// <value><c>true</c> if this data store can be used in the specified method; otherwise, <c>false</c>.</value>
		bool IsUsable(MethodDef method);

		/// <summary>
		///     Creates an accessor of this data store for the specified method.
		/// </summary>
		/// <param name="method">The method.</param>
		/// <param name="keys">The keys.</param>
		/// <param name="data">The data to store.</param>
		/// <returns>A newly accessor of this data store.</returns>
		IDataStoreAccessor CreateAccessor(MethodDef method, uint[] keys, byte[] data);
	}

	/// <summary>
	///     An accessor of data store.
	/// </summary>
	public interface IDataStoreAccessor {
		/// <summary>
		///     Emits the runtime instruction sequence for this accessor.
		/// </summary>
		/// <returns>An instruction sequence that returns the stored data.</returns>
		Instruction[] Emit();
	}
}