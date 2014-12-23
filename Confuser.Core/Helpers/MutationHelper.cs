using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Core.Helpers {
	/// <summary>
	///     Provides methods to mutated injected methods.
	/// </summary>
	public static class MutationHelper {
		const string mutationType = "Mutation";

		static readonly Dictionary<string, int> field2index = new Dictionary<string, int> {
			{ "KeyI0", 0 },
			{ "KeyI1", 1 },
			{ "KeyI2", 2 },
			{ "KeyI3", 3 },
			{ "KeyI4", 4 },
			{ "KeyI5", 5 },
			{ "KeyI6", 6 },
			{ "KeyI7", 7 },
			{ "KeyI8", 8 },
			{ "KeyI9", 9 },
			{ "KeyI10", 10 },
			{ "KeyI11", 11 },
			{ "KeyI12", 12 },
			{ "KeyI13", 13 },
			{ "KeyI14", 14 },
			{ "KeyI15", 15 }
		};

		/// <summary>
		///     Replaces the mutation key placeholder in method with actual key.
		/// </summary>
		/// <param name="method">The method to process.</param>
		/// <param name="keyId">The mutation key ID.</param>
		/// <param name="key">The actual key.</param>
		public static void InjectKey(MethodDef method, int keyId, int key) {
			foreach (Instruction instr in method.Body.Instructions) {
				if (instr.OpCode == OpCodes.Ldsfld) {
					var field = (IField)instr.Operand;
					int _keyId;
					if (field.DeclaringType.FullName == mutationType &&
					    field2index.TryGetValue(field.Name, out _keyId) &&
					    _keyId == keyId) {
						instr.OpCode = OpCodes.Ldc_I4;
						instr.Operand = key;
					}
				}
			}
		}

		/// <summary>
		///     Replaces the mutation key placeholders in method with actual keys.
		/// </summary>
		/// <param name="method">The method to process.</param>
		/// <param name="keyIds">The mutation key IDs.</param>
		/// <param name="keys">The actual keys.</param>
		public static void InjectKeys(MethodDef method, int[] keyIds, int[] keys) {
			foreach (Instruction instr in method.Body.Instructions) {
				if (instr.OpCode == OpCodes.Ldsfld) {
					var field = (IField)instr.Operand;
					int _keyIndex;
					if (field.DeclaringType.FullName == mutationType &&
					    field2index.TryGetValue(field.Name, out _keyIndex) &&
					    (_keyIndex = Array.IndexOf(keyIds, _keyIndex)) != -1) {
						instr.OpCode = OpCodes.Ldc_I4;
						instr.Operand = keys[_keyIndex];
					}
				}
			}
		}

		/// <summary>
		///     Replaces the placeholder call in method with actual instruction sequence.
		/// </summary>
		/// <param name="method">The methodto process.</param>
		/// <param name="repl">The function replacing the argument of placeholder call with actual instruction sequence.</param>
		public static void ReplacePlaceholder(MethodDef method, Func<Instruction[], Instruction[]> repl) {
			MethodTrace trace = new MethodTrace(method).Trace();
			for (int i = 0; i < method.Body.Instructions.Count; i++) {
				Instruction instr = method.Body.Instructions[i];
				if (instr.OpCode == OpCodes.Call) {
					var operand = (IMethod)instr.Operand;
					if (operand.DeclaringType.FullName == mutationType &&
					    operand.Name == "Placeholder") {
						int[] argIndexes = trace.TraceArguments(instr);
						if (argIndexes == null)
							throw new ArgumentException("Failed to trace placeholder argument.");

						int argIndex = argIndexes[0];
						Instruction[] arg = method.Body.Instructions.Skip(argIndex).Take(i - argIndex).ToArray();
						for (int j = 0; j < arg.Length; j++)
							method.Body.Instructions.RemoveAt(argIndex);
						method.Body.Instructions.RemoveAt(argIndex);
						arg = repl(arg);
						for (int j = arg.Length - 1; j >= 0; j--)
							method.Body.Instructions.Insert(argIndex, arg[j]);
						return;
					}
				}
			}
		}
	}
}