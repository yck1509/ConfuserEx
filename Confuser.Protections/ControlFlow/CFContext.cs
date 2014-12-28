using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow {
	internal enum CFType {
		Switch,
		Jump
	}

	internal enum PredicateType {
		Normal,
		Expression,
		x86
	}

	internal class CFContext {
		public ConfuserContext Context;
		public ControlFlowProtection Protection;
		public int Depth;
		public IDynCipherService DynCipher;

		public double Intensity;
		public bool JunkCode;
		public MethodDef Method;
		public PredicateType Predicate;
		public RandomGenerator Random;
		public CFType Type;

		public void AddJump(IList<Instruction> instrs, Instruction target) {
			if (!Method.Module.IsClr40 && JunkCode &&
			    !Method.DeclaringType.HasGenericParameters && !Method.HasGenericParameters &&
			    (instrs[0].OpCode.FlowControl == FlowControl.Call || instrs[0].OpCode.FlowControl == FlowControl.Next)) {
				switch (Random.NextInt32(3)) {
					case 0:
						instrs.Add(Instruction.Create(OpCodes.Ldc_I4_0));
						instrs.Add(Instruction.Create(OpCodes.Brtrue, instrs[0]));
						break;

					case 1:
						instrs.Add(Instruction.Create(OpCodes.Ldc_I4_1));
						instrs.Add(Instruction.Create(OpCodes.Brfalse, instrs[0]));
						break;

					case 2: // Take that, de4dot + ILSpy :)
						bool addDefOk = false;
						if (Random.NextBoolean()) {
							TypeDef randomType;
							randomType = Method.Module.Types[Random.NextInt32(Method.Module.Types.Count)];

							if (randomType.HasMethods) {
								instrs.Add(Instruction.Create(OpCodes.Ldtoken, randomType.Methods[Random.NextInt32(randomType.Methods.Count)]));
								instrs.Add(Instruction.Create(OpCodes.Box, Method.Module.CorLibTypes.GetTypeRef("System", "RuntimeMethodHandle")));
								addDefOk = true;
							}
						}

						if (!addDefOk) {
							instrs.Add(Instruction.Create(OpCodes.Ldc_I4, Random.NextBoolean() ? 0 : 1));
							instrs.Add(Instruction.Create(OpCodes.Box, Method.Module.CorLibTypes.Int32.TypeDefOrRef));
						}
						Instruction pop = Instruction.Create(OpCodes.Pop);
						instrs.Add(Instruction.Create(OpCodes.Brfalse, instrs[0]));
						instrs.Add(Instruction.Create(OpCodes.Ldc_I4, Random.NextBoolean() ? 0 : 1));
						instrs.Add(pop);
						break;
				}
			}

			instrs.Add(Instruction.Create(OpCodes.Br, target));
		}

		public void AddJunk(IList<Instruction> instrs) {
			if (Method.Module.IsClr40 || !JunkCode)
				return;

			switch (Random.NextInt32(6)) {
				case 0:
					instrs.Add(Instruction.Create(OpCodes.Pop));
					break;
				case 1:
					instrs.Add(Instruction.Create(OpCodes.Dup));
					break;
				case 2:
					instrs.Add(Instruction.Create(OpCodes.Throw));
					break;
				case 3:
					instrs.Add(Instruction.Create(OpCodes.Ldarg, new Parameter(0xff)));
					break;
				case 4:
					instrs.Add(Instruction.Create(OpCodes.Ldloc, new Local(null) { Index = 0xff }));
					break;
				case 5:
					instrs.Add(Instruction.Create(OpCodes.Ldtoken, Method));
					break;
			}
		}
	}
}