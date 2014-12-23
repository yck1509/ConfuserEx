using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Compress {
	internal class NormalDeriver : IKeyDeriver {
		uint k1;
		uint k2;
		uint k3;
		uint seed;

		public void Init(ConfuserContext ctx, RandomGenerator random) {
			k1 = random.NextUInt32() | 1;
			k2 = random.NextUInt32() | 1;
			k3 = random.NextUInt32() | 1;
			seed = random.NextUInt32();
		}

		public uint[] DeriveKey(uint[] a, uint[] b) {
			var ret = new uint[0x10];
			var state = seed;
			for (int i = 0; i < 0x10; i++) {
				switch (state % 3) {
					case 0:
						ret[i] = a[i] ^ b[i];
						break;
					case 1:
						ret[i] = a[i] * b[i];
						break;
					case 2:
						ret[i] = a[i] + b[i];
						break;
				}
				state = (state * state) % 0x2E082D35;
				switch (state % 3) {
					case 0:
						ret[i] += k1;
						break;
					case 1:
						ret[i] ^= k2;
						break;
					case 2:
						ret[i] *= k3;
						break;
				}
				state = (state * state) % 0x2E082D35;
			}
			return ret;
		}

		public IEnumerable<Instruction> EmitDerivation(MethodDef method, ConfuserContext ctx, Local dst, Local src) {
			var state = seed;
			for (int i = 0; i < 0x10; i++) {
				yield return Instruction.Create(OpCodes.Ldloc, dst);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldloc, dst);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldelem_U4);
				yield return Instruction.Create(OpCodes.Ldloc, src);
				yield return Instruction.Create(OpCodes.Ldc_I4, i);
				yield return Instruction.Create(OpCodes.Ldelem_U4);
				switch (state % 3) {
					case 0:
						yield return Instruction.Create(OpCodes.Xor);
						break;
					case 1:
						yield return Instruction.Create(OpCodes.Mul);
						break;
					case 2:
						yield return Instruction.Create(OpCodes.Add);
						break;
				}
				state = (state * state) % 0x2E082D35;
				switch (state % 3) {
					case 0:
						yield return Instruction.Create(OpCodes.Ldc_I4, (int)k1);
						yield return Instruction.Create(OpCodes.Add);
						break;
					case 1:
						yield return Instruction.Create(OpCodes.Ldc_I4, (int)k2);
						yield return Instruction.Create(OpCodes.Xor);
						break;
					case 2:
						yield return Instruction.Create(OpCodes.Ldc_I4, (int)k3);
						yield return Instruction.Create(OpCodes.Mul);
						break;
				}
				state = (state * state) % 0x2E082D35;
				yield return Instruction.Create(OpCodes.Stelem_I4);
			}
		}
	}
}