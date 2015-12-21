using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal class EncodePhase : ProtectionPhase {
		public EncodePhase(ConstantProtection parent)
			: base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Methods; }
		}

		public override string Name {
			get { return "Constants encoding"; }
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			var moduleCtx = context.Annotations.Get<CEContext>(context.CurrentModule, ConstantProtection.ContextKey);
			if (!parameters.Targets.Any() || moduleCtx == null)
				return;

			var ldc = new Dictionary<object, List<Tuple<MethodDef, Instruction>>>();
			var ldInit = new Dictionary<byte[], List<Tuple<MethodDef, Instruction>>>(new ByteArrayComparer());

			// Extract constants
			ExtractConstants(context, parameters, moduleCtx, ldc, ldInit);

			// Encode constants
			moduleCtx.ReferenceRepl = new Dictionary<MethodDef, List<Tuple<Instruction, uint, IMethod>>>();
			moduleCtx.EncodedBuffer = new List<uint>();
			foreach (var entry in ldInit.WithProgress(context.Logger)) // Ensure the array length haven't been encoded yet
			{
				EncodeInitializer(moduleCtx, entry.Key, entry.Value);
				context.CheckCancellation();
			}
			foreach (var entry in ldc.WithProgress(context.Logger)) {
				if (entry.Key is string) {
					EncodeString(moduleCtx, (string)entry.Key, entry.Value);
				}
				else if (entry.Key is int) {
					EncodeConstant32(moduleCtx, (uint)(int)entry.Key, context.CurrentModule.CorLibTypes.Int32, entry.Value);
				}
				else if (entry.Key is long) {
					EncodeConstant64(moduleCtx, (uint)((long)entry.Key >> 32), (uint)(long)entry.Key, context.CurrentModule.CorLibTypes.Int64, entry.Value);
				}
				else if (entry.Key is float) {
					var t = new RTransform();
					t.R4 = (float)entry.Key;
					EncodeConstant32(moduleCtx, t.Hi, context.CurrentModule.CorLibTypes.Single, entry.Value);
				}
				else if (entry.Key is double) {
					var t = new RTransform();
					t.R8 = (double)entry.Key;
					EncodeConstant64(moduleCtx, t.Hi, t.Lo, context.CurrentModule.CorLibTypes.Double, entry.Value);
				}
				else
					throw new UnreachableException();
				context.CheckCancellation();
			}
			ReferenceReplacer.ReplaceReference(moduleCtx, parameters);

			// compress
			var encodedBuff = new byte[moduleCtx.EncodedBuffer.Count * 4];
			int buffIndex = 0;
			foreach (uint dat in moduleCtx.EncodedBuffer) {
				encodedBuff[buffIndex++] = (byte)((dat >> 0) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 8) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 16) & 0xff);
				encodedBuff[buffIndex++] = (byte)((dat >> 24) & 0xff);
			}
			Debug.Assert(buffIndex == encodedBuff.Length);
			encodedBuff = context.Registry.GetService<ICompressionService>().Compress(encodedBuff);
			context.CheckCancellation();

			uint compressedLen = (uint)(encodedBuff.Length + 3) / 4;
			compressedLen = (compressedLen + 0xfu) & ~0xfu;
			var compressedBuff = new uint[compressedLen];
			Buffer.BlockCopy(encodedBuff, 0, compressedBuff, 0, encodedBuff.Length);
			Debug.Assert(compressedLen % 0x10 == 0);

			// encrypt
			uint keySeed = moduleCtx.Random.NextUInt32();
			var key = new uint[0x10];
			uint state = keySeed;
			for (int i = 0; i < 0x10; i++) {
				state ^= state >> 12;
				state ^= state << 25;
				state ^= state >> 27;
				key[i] = state;
			}

			var encryptedBuffer = new byte[compressedBuff.Length * 4];
			buffIndex = 0;
			while (buffIndex < compressedBuff.Length) {
				uint[] enc = moduleCtx.ModeHandler.Encrypt(compressedBuff, buffIndex, key);
				for (int j = 0; j < 0x10; j++)
					key[j] ^= compressedBuff[buffIndex + j];
				Buffer.BlockCopy(enc, 0, encryptedBuffer, buffIndex * 4, 0x40);
				buffIndex += 0x10;
			}
			Debug.Assert(buffIndex == compressedBuff.Length);

			moduleCtx.DataField.InitialValue = encryptedBuffer;
			moduleCtx.DataField.HasFieldRVA = true;
			moduleCtx.DataType.ClassLayout = new ClassLayoutUser(0, (uint)encryptedBuffer.Length);
			MutationHelper.InjectKeys(moduleCtx.InitMethod,
			                          new[] { 0, 1 },
			                          new[] { encryptedBuffer.Length / 4, (int)keySeed });
			MutationHelper.ReplacePlaceholder(moduleCtx.InitMethod, arg => {
				var repl = new List<Instruction>();
				repl.AddRange(arg);
				repl.Add(Instruction.Create(OpCodes.Dup));
				repl.Add(Instruction.Create(OpCodes.Ldtoken, moduleCtx.DataField));
				repl.Add(Instruction.Create(OpCodes.Call, moduleCtx.Module.Import(
					typeof(RuntimeHelpers).GetMethod("InitializeArray"))));
				return repl.ToArray();
			});
		}

		void EncodeString(CEContext moduleCtx, string value, List<Tuple<MethodDef, Instruction>> references) {
			int buffIndex = EncodeByteArray(moduleCtx, Encoding.UTF8.GetBytes(value));

			UpdateReference(moduleCtx, moduleCtx.Module.CorLibTypes.String, references, buffIndex, desc => desc.StringID);
		}

		void EncodeConstant32(CEContext moduleCtx, uint value, TypeSig valueType, List<Tuple<MethodDef, Instruction>> references) {
			int buffIndex = moduleCtx.EncodedBuffer.IndexOf(value);
			if (buffIndex == -1) {
				buffIndex = moduleCtx.EncodedBuffer.Count;
				moduleCtx.EncodedBuffer.Add(value);
			}

			UpdateReference(moduleCtx, valueType, references, buffIndex, desc => desc.NumberID);
		}

		void EncodeConstant64(CEContext moduleCtx, uint hi, uint lo, TypeSig valueType, List<Tuple<MethodDef, Instruction>> references) {
			int buffIndex = -1;
			do {
				buffIndex = moduleCtx.EncodedBuffer.IndexOf(lo, buffIndex + 1);
				if (buffIndex + 1 < moduleCtx.EncodedBuffer.Count && moduleCtx.EncodedBuffer[buffIndex + 1] == hi)
					break;
			} while (buffIndex >= 0);
			
			if (buffIndex == -1) {
				buffIndex = moduleCtx.EncodedBuffer.Count;
				moduleCtx.EncodedBuffer.Add(lo);
				moduleCtx.EncodedBuffer.Add(hi);
			}

			UpdateReference(moduleCtx, valueType, references, buffIndex, desc => desc.NumberID);
		}

		void EncodeInitializer(CEContext moduleCtx, byte[] init, List<Tuple<MethodDef, Instruction>> references) {
			int buffIndex = -1;

			foreach (var instr in references) {
				IList<Instruction> instrs = instr.Item1.Body.Instructions;
				int i = instrs.IndexOf(instr.Item2);

				if (buffIndex == -1)
					buffIndex = EncodeByteArray(moduleCtx, init);

				Tuple<MethodDef, DecoderDesc> decoder = moduleCtx.Decoders[moduleCtx.Random.NextInt32(moduleCtx.Decoders.Count)];
				uint id = (uint)buffIndex | (uint)(decoder.Item2.InitializerID << 30);
				id = moduleCtx.ModeHandler.Encode(decoder.Item2.Data, moduleCtx, id);

				instrs[i - 4].Operand = (int)id;
				instrs[i - 3].OpCode = OpCodes.Call;
				var arrType = new SZArraySig(((ITypeDefOrRef)instrs[i - 3].Operand).ToTypeSig());
				instrs[i - 3].Operand = new MethodSpecUser(decoder.Item1, new GenericInstMethodSig(arrType));
				instrs.RemoveAt(i - 2);
				instrs.RemoveAt(i - 2);
				instrs.RemoveAt(i - 2);
			}
		}

		int EncodeByteArray(CEContext moduleCtx, byte[] buff) {
			int buffIndex = moduleCtx.EncodedBuffer.Count;
			moduleCtx.EncodedBuffer.Add((uint)buff.Length);

			// byte[] -> uint[]
			int integral = buff.Length / 4, remainder = buff.Length % 4;
			for (int i = 0; i < integral; i++) {
				var data = (uint)(buff[i * 4] | (buff[i * 4 + 1] << 8) | (buff[i * 4 + 2] << 16) | (buff[i * 4 + 3] << 24));
				moduleCtx.EncodedBuffer.Add(data);
			}
			if (remainder > 0) {
				int baseIndex = integral * 4;
				uint r = 0;
				for (int i = 0; i < remainder; i++)
					r |= (uint)(buff[baseIndex + i] << (i * 8));
				moduleCtx.EncodedBuffer.Add(r);
			}
			return buffIndex;
		}

		void UpdateReference(CEContext moduleCtx, TypeSig valueType, List<Tuple<MethodDef, Instruction>> references, int buffIndex, Func<DecoderDesc, byte> typeID) {
			foreach (var instr in references) {
				Tuple<MethodDef, DecoderDesc> decoder = moduleCtx.Decoders[moduleCtx.Random.NextInt32(moduleCtx.Decoders.Count)];
				uint id = (uint)buffIndex | (uint)(typeID(decoder.Item2) << 30);
				id = moduleCtx.ModeHandler.Encode(decoder.Item2.Data, moduleCtx, id);

				var targetDecoder = new MethodSpecUser(decoder.Item1, new GenericInstMethodSig(valueType));
				moduleCtx.ReferenceRepl.AddListEntry(instr.Item1, Tuple.Create(instr.Item2, id, (IMethod)targetDecoder));
			}
		}

		void ExtractConstants(
			ConfuserContext context, ProtectionParameters parameters, CEContext moduleCtx,
			Dictionary<object, List<Tuple<MethodDef, Instruction>>> ldc,
			Dictionary<byte[], List<Tuple<MethodDef, Instruction>>> ldInit) {
			foreach (MethodDef method in parameters.Targets.OfType<MethodDef>().WithProgress(context.Logger)) {
				if (!method.HasBody)
					continue;

				moduleCtx.Elements = 0;
				string elements = parameters.GetParameter(context, method, "elements", "SI");
				foreach (char elem in elements)
					switch (elem) {
						case 'S':
						case 's':
							moduleCtx.Elements |= EncodeElements.Strings;
							break;
						case 'N':
						case 'n':
							moduleCtx.Elements |= EncodeElements.Numbers;
							break;
						case 'P':
						case 'p':
							moduleCtx.Elements |= EncodeElements.Primitive;
							break;
						case 'I':
						case 'i':
							moduleCtx.Elements |= EncodeElements.Initializers;
							break;
					}

				if (moduleCtx.Elements == 0)
					continue;

				foreach (Instruction instr in method.Body.Instructions) {
					bool eligible = false;
					if (instr.OpCode == OpCodes.Ldstr && (moduleCtx.Elements & EncodeElements.Strings) != 0) {
						var operand = (string)instr.Operand;
						if (string.IsNullOrEmpty(operand) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
							continue;
						eligible = true;
					}
					else if (instr.OpCode == OpCodes.Call && (moduleCtx.Elements & EncodeElements.Initializers) != 0) {
						var operand = (IMethod)instr.Operand;
						if (operand.DeclaringType.DefinitionAssembly.IsCorLib() &&
						    operand.DeclaringType.Namespace == "System.Runtime.CompilerServices" &&
						    operand.DeclaringType.Name == "RuntimeHelpers" &&
						    operand.Name == "InitializeArray") {
							IList<Instruction> instrs = method.Body.Instructions;
							int i = instrs.IndexOf(instr);
							if (instrs[i - 1].OpCode != OpCodes.Ldtoken) continue;
							if (instrs[i - 2].OpCode != OpCodes.Dup) continue;
							if (instrs[i - 3].OpCode != OpCodes.Newarr) continue;
							if (instrs[i - 4].OpCode != OpCodes.Ldc_I4) continue;

							var dataField = instrs[i - 1].Operand as FieldDef;
							if (dataField == null)
								continue;
							if (!dataField.HasFieldRVA || dataField.InitialValue == null)
								continue;

							// Prevent array length from being encoded
							var arrLen = (int)instrs[i - 4].Operand;
							if (ldc.ContainsKey(arrLen)) {
								List<Tuple<MethodDef, Instruction>> list = ldc[arrLen];
								list.RemoveWhere(entry => entry.Item2 == instrs[i - 4]);
								if (list.Count == 0)
									ldc.Remove(arrLen);
							}

                            if(dataField.DeclaringType!=null)
							    dataField.DeclaringType.Fields.Remove(dataField);
							var value = new byte[dataField.InitialValue.Length + 4];
							value[0] = (byte)(arrLen >> 0);
							value[1] = (byte)(arrLen >> 8);
							value[2] = (byte)(arrLen >> 16);
							value[3] = (byte)(arrLen >> 24);
							Buffer.BlockCopy(dataField.InitialValue, 0, value, 4, dataField.InitialValue.Length);
							ldInit.AddListEntry(value, Tuple.Create(method, instr));
						}
					}
					else if ((moduleCtx.Elements & EncodeElements.Numbers) != 0) {
						if (instr.OpCode == OpCodes.Ldc_I4) {
							var operand = (int)instr.Operand;
							if ((operand >= -1 && operand <= 8) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
								continue;
							eligible = true;
						}
						else if (instr.OpCode == OpCodes.Ldc_I8) {
							var operand = (long)instr.Operand;
							if ((operand >= -1 && operand <= 1) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
								continue;
							eligible = true;
						}
						else if (instr.OpCode == OpCodes.Ldc_R4) {
							var operand = (float)instr.Operand;
							if ((operand == -1 || operand == 0 || operand == 1) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
								continue;
							eligible = true;
						}
						else if (instr.OpCode == OpCodes.Ldc_R8) {
							var operand = (double)instr.Operand;
							if ((operand == -1 || operand == 0 || operand == 1) && (moduleCtx.Elements & EncodeElements.Primitive) == 0)
								continue;
							eligible = true;
						}
					}

					if (eligible)
						ldc.AddListEntry(instr.Operand, Tuple.Create(method, instr));
				}

				context.CheckCancellation();
			}
		}

		class ByteArrayComparer : IEqualityComparer<byte[]> {
			public bool Equals(byte[] x, byte[] y) {
				return x.SequenceEqual(y);
			}

			public int GetHashCode(byte[] obj) {
				int ret = 31;
				foreach (byte v in obj)
					ret = ret * 17 + v;
				return ret;
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		struct RTransform {
			[FieldOffset(0)] public float R4;
			[FieldOffset(0)] public double R8;

			[FieldOffset(0)] public readonly uint Hi;
			[FieldOffset(4)] public readonly uint Lo;
		}
	}
}