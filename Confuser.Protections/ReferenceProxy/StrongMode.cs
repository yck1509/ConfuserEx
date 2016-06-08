using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.ReferenceProxy {
	internal class StrongMode : RPMode {
		// { invoke opCode, invoke target, encoding}, { proxy field, bridge method }
		readonly List<FieldDesc> fieldDescs = new List<FieldDesc>();
		readonly Dictionary<Tuple<Code, IMethod, IRPEncoding>, Tuple<FieldDef, MethodDef>> fields = new Dictionary<Tuple<Code, IMethod, IRPEncoding>, Tuple<FieldDef, MethodDef>>();

		readonly Dictionary<IRPEncoding, InitMethodDesc[]> inits = new Dictionary<IRPEncoding, InitMethodDesc[]>();
		RPContext encodeCtx;
		Tuple<TypeDef, Func<int, int>>[] keyAttrs;

		static int? TraceBeginning(RPContext ctx, int index, int argCount) {
			if (ctx.BranchTargets.Contains(ctx.Body.Instructions[index]))
				return null;

			int currentStack = argCount;
			int currentIndex = index;
			while (currentStack > 0) {
				currentIndex--;
				Instruction currentInstr = ctx.Body.Instructions[currentIndex];

				// Disrupt stack analysis :/ Used by array initializer
				if (currentInstr.OpCode == OpCodes.Pop || currentInstr.OpCode == OpCodes.Dup)
					return null;

				// No branch instr.
				switch (currentInstr.OpCode.FlowControl) {
					case FlowControl.Call:
					case FlowControl.Break:
					case FlowControl.Meta:
					case FlowControl.Next:
						break;
					default:
						return null;
				}

				int push, pop;
				currentInstr.CalculateStackUsage(out push, out pop);
				currentStack += pop;
				currentStack -= push;

				// No branch target
				if (ctx.BranchTargets.Contains(currentInstr) && currentStack != 0)
					return null;
			}
			if (currentStack < 0)
				return null;
			return currentIndex;
		}

		public override void ProcessCall(RPContext ctx, int instrIndex) {
			Instruction invoke = ctx.Body.Instructions[instrIndex];

			TypeDef declType = ((IMethod)invoke.Operand).DeclaringType.ResolveTypeDefThrow();
			if (!declType.Module.IsILOnly) // Reflection doesn't like mixed mode modules.
				return;
			if (declType.IsGlobalModuleType) // Reflection doesn't like global methods too.
				return;

			int push, pop;
			invoke.CalculateStackUsage(out push, out pop);
			int? begin = TraceBeginning(ctx, instrIndex, pop);
			// Fail to trace the arguments => fall back to bridge method
			bool fallBack = begin == null;

			if (fallBack) {
				ProcessBridge(ctx, instrIndex);
			}
			else {
				ProcessInvoke(ctx, instrIndex, begin.Value);
			}
		}

		void ProcessBridge(RPContext ctx, int instrIndex) {
			Instruction instr = ctx.Body.Instructions[instrIndex];
			var target = (IMethod)instr.Operand;

			TypeDef declType = target.DeclaringType.ResolveTypeDefThrow();
			if (!declType.Module.IsILOnly) // Reflection doesn't like mixed mode modules.
				return;
			if (declType.IsGlobalModuleType) // Reflection doesn't like global methods too.
				return;

			Tuple<Code, IMethod, IRPEncoding> key = Tuple.Create(instr.OpCode.Code, target, ctx.EncodingHandler);
			Tuple<FieldDef, MethodDef> proxy;
			if (fields.TryGetValue(key, out proxy)) {
				if (proxy.Item2 != null) {
					instr.OpCode = OpCodes.Call;
					instr.Operand = proxy.Item2;
					return;
				}
			}
			else
				proxy = new Tuple<FieldDef, MethodDef>(null, null);

			MethodSig sig = CreateProxySignature(ctx, target, instr.OpCode.Code == Code.Newobj);
			TypeDef delegateType = GetDelegateType(ctx, sig);

			// Create proxy field
			if (proxy.Item1 == null)
				proxy = new Tuple<FieldDef, MethodDef>(
					CreateField(ctx, delegateType),
					proxy.Item2);

			// Create proxy bridge
			Debug.Assert(proxy.Item2 == null);

			proxy = new Tuple<FieldDef, MethodDef>(
				proxy.Item1,
				CreateBridge(ctx, delegateType, proxy.Item1, sig));

			fields[key] = proxy;

			// Replace instruction
			instr.OpCode = OpCodes.Call;
			instr.Operand = proxy.Item2;

			var targetDef = target.ResolveMethodDef();
			if (targetDef != null)
				ctx.Context.Annotations.Set(targetDef, ReferenceProxyProtection.Targeted, ReferenceProxyProtection.Targeted);
		}

		void ProcessInvoke(RPContext ctx, int instrIndex, int argBeginIndex) {
			Instruction instr = ctx.Body.Instructions[instrIndex];
			var target = (IMethod)instr.Operand;

			MethodSig sig = CreateProxySignature(ctx, target, instr.OpCode.Code == Code.Newobj);
			TypeDef delegateType = GetDelegateType(ctx, sig);

			Tuple<Code, IMethod, IRPEncoding> key = Tuple.Create(instr.OpCode.Code, target, ctx.EncodingHandler);
			Tuple<FieldDef, MethodDef> proxy;
			if (!fields.TryGetValue(key, out proxy)) {
				// Create proxy field
				proxy = new Tuple<FieldDef, MethodDef>(CreateField(ctx, delegateType), null);
				fields[key] = proxy;
			}

			// Insert field load & replace instruction
			if (argBeginIndex == instrIndex) {
				ctx.Body.Instructions.Insert(instrIndex + 1,
				                             new Instruction(OpCodes.Call, delegateType.FindMethod("Invoke")));
				instr.OpCode = OpCodes.Ldsfld;
				instr.Operand = proxy.Item1;
			}
			else {
				Instruction argBegin = ctx.Body.Instructions[argBeginIndex];
				ctx.Body.Instructions.Insert(argBeginIndex + 1,
				                             new Instruction(argBegin.OpCode, argBegin.Operand));
				argBegin.OpCode = OpCodes.Ldsfld;
				argBegin.Operand = proxy.Item1;

				instr.OpCode = OpCodes.Call;
				instr.Operand = delegateType.FindMethod("Invoke");
			}

			var targetDef = target.ResolveMethodDef();
			if (targetDef != null)
				ctx.Context.Annotations.Set(targetDef, ReferenceProxyProtection.Targeted, ReferenceProxyProtection.Targeted);
		}

		MethodDef CreateBridge(RPContext ctx, TypeDef delegateType, FieldDef field, MethodSig sig) {
			var method = new MethodDefUser(ctx.Name.RandomName(), sig);
			method.Attributes = MethodAttributes.PrivateScope | MethodAttributes.Static;
			method.ImplAttributes = MethodImplAttributes.Managed | MethodImplAttributes.IL;

			method.Body = new CilBody();
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldsfld, field));
			for (int i = 0; i < method.Parameters.Count; i++)
				method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, method.Parameters[i]));
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, delegateType.FindMethod("Invoke")));
			method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

			delegateType.Methods.Add(method);

			ctx.Context.Registry.GetService<IMarkerService>().Mark(method, ctx.Protection);
			ctx.Name.SetCanRename(method, false);

			return method;
		}

		FieldDef CreateField(RPContext ctx, TypeDef delegateType) {
			// Details will be filled in during metadata writing
			TypeDef randomType;
			do {
				randomType = ctx.Module.Types[ctx.Random.NextInt32(ctx.Module.Types.Count)];
			} while (randomType.HasGenericParameters || randomType.IsGlobalModuleType || randomType.IsDelegate());

			TypeSig fieldType = new CModOptSig(randomType, delegateType.ToTypeSig());

			var field = new FieldDefUser("", new FieldSig(fieldType), FieldAttributes.Static | FieldAttributes.Assembly);
			field.CustomAttributes.Add(new CustomAttribute(GetKeyAttr(ctx).FindInstanceConstructors().First()));
			delegateType.Fields.Add(field);

			ctx.Marker.Mark(field, ctx.Protection);
			ctx.Name.SetCanRename(field, false);

			return field;
		}

		TypeDef GetKeyAttr(RPContext ctx) {
			if (keyAttrs == null)
				keyAttrs = new Tuple<TypeDef, Func<int, int>>[0x10];

			int index = ctx.Random.NextInt32(keyAttrs.Length);
			if (keyAttrs[index] == null) {
				TypeDef rtType = ctx.Context.Registry.GetService<IRuntimeService>().GetRuntimeType("Confuser.Runtime.RefProxyKey");
				TypeDef injectedAttr = InjectHelper.Inject(rtType, ctx.Module);
				injectedAttr.Name = ctx.Name.RandomName();
				injectedAttr.Namespace = string.Empty;

				Expression expression, inverse;
				var var = new Variable("{VAR}");
				var result = new Variable("{RESULT}");

				ctx.DynCipher.GenerateExpressionPair(
					ctx.Random,
					new VariableExpression { Variable = var }, new VariableExpression { Variable = result },
					ctx.Depth, out expression, out inverse);

				var expCompiled = new DMCodeGen(typeof(int), new[] { Tuple.Create("{VAR}", typeof(int)) })
					.GenerateCIL(expression)
					.Compile<Func<int, int>>();

				MethodDef ctor = injectedAttr.FindMethod(".ctor");
				MutationHelper.ReplacePlaceholder(ctor, arg => {
					var invCompiled = new List<Instruction>();
					new CodeGen(arg, ctor, invCompiled).GenerateCIL(inverse);
					return invCompiled.ToArray();
				});
				keyAttrs[index] = Tuple.Create(injectedAttr, expCompiled);

				ctx.Module.AddAsNonNestedType(injectedAttr);

				foreach (IDnlibDef def in injectedAttr.FindDefinitions()) {
					if (def.Name == "GetHashCode") {
						ctx.Name.MarkHelper(def, ctx.Marker, ctx.Protection);
						((MethodDef)def).Access = MethodAttributes.Public;
					}
					else
						ctx.Name.MarkHelper(def, ctx.Marker, ctx.Protection);
				}
			}
			return keyAttrs[index].Item1;
		}

		InitMethodDesc GetInitMethod(RPContext ctx, IRPEncoding encoding) {
			InitMethodDesc[] initDescs;
			if (!inits.TryGetValue(encoding, out initDescs))
				inits[encoding] = initDescs = new InitMethodDesc[ctx.InitCount];

			int index = ctx.Random.NextInt32(initDescs.Length);
			if (initDescs[index] == null) {
				TypeDef rtType = ctx.Context.Registry.GetService<IRuntimeService>().GetRuntimeType("Confuser.Runtime.RefProxyStrong");
				MethodDef injectedMethod = InjectHelper.Inject(rtType.FindMethod("Initialize"), ctx.Module);
				ctx.Module.GlobalType.Methods.Add(injectedMethod);

				injectedMethod.Access = MethodAttributes.PrivateScope;
				injectedMethod.Name = ctx.Name.RandomName();
				ctx.Name.SetCanRename(injectedMethod, false);
				ctx.Marker.Mark(injectedMethod, ctx.Protection);

				var desc = new InitMethodDesc { Method = injectedMethod };

				// Field name has five bytes, each bytes has different order & meaning
				int[] order = Enumerable.Range(0, 5).ToArray();
				ctx.Random.Shuffle(order);
				desc.OpCodeIndex = order[4];

				desc.TokenNameOrder = new int[4];
				Array.Copy(order, 0, desc.TokenNameOrder, 0, 4);
				desc.TokenByteOrder = Enumerable.Range(0, 4).Select(x => x * 8).ToArray();
				ctx.Random.Shuffle(desc.TokenByteOrder);

				var keyInjection = new int[9];
				Array.Copy(desc.TokenNameOrder, 0, keyInjection, 0, 4);
				Array.Copy(desc.TokenByteOrder, 0, keyInjection, 4, 4);
				keyInjection[8] = desc.OpCodeIndex;
				MutationHelper.InjectKeys(injectedMethod, Enumerable.Range(0, 9).ToArray(), keyInjection);

				// Encoding
				MutationHelper.ReplacePlaceholder(injectedMethod, arg => { return encoding.EmitDecode(injectedMethod, ctx, arg); });
				desc.Encoding = encoding;

				initDescs[index] = desc;
			}
			return initDescs[index];
		}

		public override void Finalize(RPContext ctx) {
			foreach (var field in fields) {
				InitMethodDesc init = GetInitMethod(ctx, field.Key.Item3);
				byte opKey;
				do {
					// No zero bytes
					opKey = ctx.Random.NextByte();
				} while (opKey == (byte)field.Key.Item1);

				TypeDef delegateType = field.Value.Item1.DeclaringType;

				MethodDef cctor = delegateType.FindOrCreateStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init.Method));
				cctor.Body.Instructions.Insert(0, Instruction.CreateLdcI4(opKey));
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldtoken, field.Value.Item1));

				fieldDescs.Add(new FieldDesc {
					Field = field.Value.Item1,
					OpCode = field.Key.Item1,
					Method = field.Key.Item2,
					OpKey = opKey,
					InitDesc = init
				});
			}

			foreach (TypeDef delegateType in ctx.Delegates.Values) {
				MethodDef cctor = delegateType.FindOrCreateStaticConstructor();
				ctx.Marker.Mark(cctor, ctx.Protection);
				ctx.Name.SetCanRename(cctor, false);
			}

			ctx.Context.CurrentModuleWriterOptions.MetaDataOptions.Flags |= MetaDataFlags.PreserveExtraSignatureData;
			ctx.Context.CurrentModuleWriterListener.OnWriterEvent += EncodeField;
			encodeCtx = ctx;
		}

		void EncodeField(object sender, ModuleWriterListenerEventArgs e) {
			var writer = (ModuleWriterBase)sender;
			if (e.WriterEvent == ModuleWriterEvent.MDMemberDefRidsAllocated && keyAttrs != null) {
				Dictionary<TypeDef, Func<int, int>> keyFuncs = keyAttrs
					.Where(entry => entry != null)
					.ToDictionary(entry => entry.Item1, entry => entry.Item2);
				foreach (FieldDesc desc in fieldDescs) {
					uint token = writer.MetaData.GetToken(desc.Method).Raw;
					uint key = encodeCtx.Random.NextUInt32() | 1;

					// CA
					CustomAttribute ca = desc.Field.CustomAttributes[0];
					int encodedKey = keyFuncs[(TypeDef)ca.AttributeType]((int)MathsUtils.modInv(key));
					ca.ConstructorArguments.Add(new CAArgument(encodeCtx.Module.CorLibTypes.Int32, encodedKey));
					token *= key;

					// Encoding
					token = (uint)desc.InitDesc.Encoding.Encode(desc.InitDesc.Method, encodeCtx, (int)token);

					// Field name
					var name = new char[5];
					name[desc.InitDesc.OpCodeIndex] = (char)((byte)desc.OpCode ^ desc.OpKey);

					byte[] nameKey = encodeCtx.Random.NextBytes(4);
					uint encodedNameKey = 0;
					for (int i = 0; i < 4; i++) {
						// No zero bytes
						while (nameKey[i] == 0)
							nameKey[i] = encodeCtx.Random.NextByte();
						name[desc.InitDesc.TokenNameOrder[i]] = (char)nameKey[i];
						encodedNameKey |= (uint)nameKey[i] << desc.InitDesc.TokenByteOrder[i];
					}
					desc.Field.Name = new string(name);

					// Field sig
					FieldSig sig = desc.Field.FieldSig;
					uint encodedToken = (token - writer.MetaData.GetToken(((CModOptSig)sig.Type).Modifier).Raw) ^ encodedNameKey;


					var extra = new byte[8];
					extra[0] = 0xc0;
					extra[3] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder[3]);
					extra[4] = 0xc0;
					extra[5] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder[2]);
					extra[6] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder[1]);
					extra[7] = (byte)(encodedToken >> desc.InitDesc.TokenByteOrder[0]);
					sig.ExtraData = extra;
				}
			}
		}

		class CodeGen : CILCodeGen {
			readonly Instruction[] arg;

			public CodeGen(Instruction[] arg, MethodDef method, IList<Instruction> instrs)
				: base(method, instrs) {
				this.arg = arg;
			}

			protected override void LoadVar(Variable var) {
				if (var.Name == "{RESULT}") {
					foreach (Instruction instr in arg)
						Emit(instr);
				}
				else
					base.LoadVar(var);
			}
		}

		class FieldDesc {
			public FieldDef Field;
			public InitMethodDesc InitDesc;
			public IMethod Method;
			public Code OpCode;
			public byte OpKey;
		}

		class InitMethodDesc {
			public IRPEncoding Encoding;
			public MethodDef Method;
			public int OpCodeIndex;
			public int[] TokenByteOrder;
			public int[] TokenNameOrder;
		}
	}
}