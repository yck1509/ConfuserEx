using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Compress {
	internal class NormalDeriver : IKeyDeriver {
		private uint k1;
		private uint k2;
		private uint k3;

		public void Init(ConfuserContext ctx, RandomGenerator random) {
			k1 = random.NextUInt32() | 1;
			k2 = random.NextUInt32() | 1;
			k3 = random.NextUInt32() | 1;
		}

		public uint[] DeriveKey(uint[] a, uint[] b) {
			var ret = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				switch (i % 3) {
					case 0:
						ret[i] = (a[i] ^ b[i]) + k1;
						break;
					case 1:
						ret[i] = (a[i] * b[i]) ^ k2;
						break;
					case 2:
						ret[i] = (a[i] + b[i]) * k3;
						break;
				}
			}
			return ret;
		}

		public IEnumerable<Instruction> EmitDerivation(MethodDef method, ConfuserContext ctx, Local dst, Local src) {
			for (int i = 0; i < 0x10; i++) {
				yield return Instruction.Create(OpCodes.Ldloc, dst);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldloc, dst);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldelem_U4);
				yield return Instruction.Create(OpCodes.Ldloc, src);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldelem_U4);
				switch (i % 3) {
					case 0:
						yield return Instruction.Create(OpCodes.Xor);
						yield return Instruction.Create(OpCodes.Ldc_I4, (int)k1);
						yield return Instruction.Create(OpCodes.Add);
						break;
					case 1:
						yield return Instruction.Create(OpCodes.Mul);
						yield return Instruction.Create(OpCodes.Ldc_I4, (int)k2);
						yield return Instruction.Create(OpCodes.Xor);
						break;
					case 2:
						yield return Instruction.Create(OpCodes.Add);
						yield return Instruction.Create(OpCodes.Ldc_I4, (int)k3);
						yield return Instruction.Create(OpCodes.Mul);
						break;
				}
				yield return Instruction.Create(OpCodes.Stelem_I4);
			}
		}
	}
}