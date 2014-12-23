using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core.Helpers;
using Confuser.DynCipher;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal class NormalMode : IEncodeMode {
		public IEnumerable<Instruction> EmitDecrypt(MethodDef init, CEContext ctx, Local block, Local key) {
			for (int i = 0; i < 0x10; i++) {
				yield return Instruction.Create(OpCodes.Ldloc, block);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldloc, block);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldelem_U4);
				yield return Instruction.Create(OpCodes.Ldloc, key);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldelem_U4);
				yield return Instruction.Create(OpCodes.Xor);
				yield return Instruction.Create(OpCodes.Stelem_I4);
			}
		}

		public uint[] Encrypt(uint[] data, int offset, uint[] key) {
			var ret = new uint[key.Length];
			for (int i = 0; i < key.Length; i++)
				ret[i] = data[i + offset] ^ key[i];
			return ret;
		}

		public object CreateDecoder(MethodDef decoder, CEContext ctx) {
			uint k1 = ctx.Random.NextUInt32() | 1;
			uint k2 = ctx.Random.NextUInt32();
			MutationHelper.ReplacePlaceholder(decoder, arg => {
				var repl = new List<Instruction>();
				repl.AddRange(arg);
				repl.Add(Instruction.Create(OpCodes.Ldc_I4, (int)MathsUtils.modInv(k1)));
				repl.Add(Instruction.Create(OpCodes.Mul));
				repl.Add(Instruction.Create(OpCodes.Ldc_I4, (int)k2));
				repl.Add(Instruction.Create(OpCodes.Xor));
				return repl.ToArray();
			});
			return Tuple.Create(k1, k2);
		}

		public uint Encode(object data, CEContext ctx, uint id) {
			var key = (Tuple<uint, uint>)data;
			uint ret = (id ^ key.Item2) * key.Item1;
			Debug.Assert(((ret * MathsUtils.modInv(key.Item1)) ^ key.Item2) == id);
			return ret;
		}
	}
}