using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Core.API {
	/// <summary>
	///     The descriptor of a type of opaque predicate.
	/// </summary>
	public interface IOpaquePredicateDescriptor {
		/// <summary>
		///     Gets the type of the opaque predicate.
		/// </summary>
		/// <value>The type of the opaque predicate.</value>
		OpaquePredicateType Type { get; }

		/// <summary>
		///     Gets the number of arguments this predicate has.
		/// </summary>
		/// <remarks>
		///     When <see cref="IOpaquePredicateDescriptor.Type" /> is <see cref="OpaquePredicateType.Invariant" />,
		///     there can be 0 or more arguments.
		///     When <see cref="IOpaquePredicateDescriptor.Type" /> is <see cref="OpaquePredicateType.Function" />,
		///     there must be more than 0 arguments.
		/// </remarks>
		/// <value>The number of arguments this predicate has.</value>
		int ArgumentCount { get; }

		/// <summary>
		///     Determines whether this predicate can be used with the specified method.
		/// </summary>
		/// <param name="method">The method.</param>
		/// <value><c>true</c> if this predicate can be used with the specified method; otherwise, <c>false</c>.</value>
		bool IsUsable(MethodDef method);

		/// <summary>
		///     Creates a new opaque predicate for the specified method.
		/// </summary>
		/// <param name="method">The method.</param>
		/// <returns>A newly create opaque predicate.</returns>
		IOpaquePredicate CreatePredicate(MethodDef method);
	}

	/// <summary>
	///     An instance of opaque predicate.
	/// </summary>
	public interface IOpaquePredicate {
		/// <summary>
		///     Emits the runtime instruction sequence for this predicate.
		/// </summary>
		/// <param name="loadArg">
		///     A function that returns an instruction sequence that returns the input value,
		///     or <c>null</c> if <see cref="IOpaquePredicateDescriptor.ArgumentCount" /> is 0.
		/// </param>
		/// <returns>An instruction sequence that returns the value of this predicate.</returns>
		Instruction[] Emit(Func<Instruction[]> loadArg);

		/// <summary>
		///     Computes the value of this predicate with the specified argument.
		/// </summary>
		/// <param name="arg">The argument to this predicate.</param>
		/// <returns>The return value of this predicate.</returns>
		uint GetValue(uint[] arg);
	}

	/// <summary>
	///     The type of opaque predicate.
	/// </summary>
	public enum OpaquePredicateType {
		/// <summary>
		///     A function, in a mathematics sense, with one input and one output.
		/// </summary>
		Function,

		/// <summary>
		///     A constant function, always returning the same value.
		/// </summary>
		Invariant
	}
}