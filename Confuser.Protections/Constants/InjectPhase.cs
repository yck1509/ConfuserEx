using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;

namespace Confuser.Protections.Constants {
	internal class InjectPhase : ProtectionPhase {
		public InjectPhase(ConstantProtection parent)
			: base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Methods; }
		}

		public override string Name {
			get { return "Constant encryption helpers injection"; }
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			if (parameters.Targets.Any()) {
				var compression = context.Registry.GetService<ICompressionService>();
				var name = context.Registry.GetService<INameService>();
				var marker = context.Registry.GetService<IMarkerService>();
				var rt = context.Registry.GetService<IRuntimeService>();
				var moduleCtx = new CEContext {
					Protection = (ConstantProtection)Parent,
					Random = context.Registry.GetService<IRandomService>().GetRandomGenerator(Parent.Id),
					Context = context,
					Module = context.CurrentModule,
					Marker = marker,
					DynCipher = context.Registry.GetService<IDynCipherService>(),
					Name = name
				};

				// Extract parameters
				moduleCtx.Mode = parameters.GetParameter(context, context.CurrentModule, "mode", Mode.Normal);
				moduleCtx.DecoderCount = parameters.GetParameter(context, context.CurrentModule, "decoderCount", 5);

				switch (moduleCtx.Mode) {
					case Mode.Normal:
						moduleCtx.ModeHandler = new NormalMode();
						break;
					case Mode.Dynamic:
						moduleCtx.ModeHandler = new DynamicMode();
						break;
					case Mode.x86:
						moduleCtx.ModeHandler = new x86Mode();
						if ((context.CurrentModule.Cor20HeaderFlags & ComImageFlags.ILOnly) != 0)
							context.CurrentModuleWriterOptions.Cor20HeaderOptions.Flags &= ~ComImageFlags.ILOnly;
						break;
					default:
						throw new UnreachableException();
				}

				// Inject helpers
				MethodDef decomp = compression.GetRuntimeDecompressor(context.CurrentModule, member => {
					name.MarkHelper(member, marker, (Protection)Parent);
					if (member is MethodDef)
						ProtectionParameters.GetParameters(context, member).Remove(Parent);
				});
				InjectHelpers(context, compression, rt, moduleCtx);

				// Mutate codes
				MutateInitializer(moduleCtx, decomp);

				MethodDef cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, moduleCtx.InitMethod));

				context.Annotations.Set(context.CurrentModule, ConstantProtection.ContextKey, moduleCtx);
			}
		}

		void InjectHelpers(ConfuserContext context, ICompressionService compression, IRuntimeService rt, CEContext moduleCtx) {
			IEnumerable<IDnlibDef> members = InjectHelper.Inject(rt.GetRuntimeType("Confuser.Runtime.Constant"), context.CurrentModule.GlobalType, context.CurrentModule);
			foreach (IDnlibDef member in members) {
				if (member.Name == "Get") {
					context.CurrentModule.GlobalType.Remove((MethodDef)member);
					continue;
				}
				if (member.Name == "b")
					moduleCtx.BufferField = (FieldDef)member;
				else if (member.Name == "Initialize")
					moduleCtx.InitMethod = (MethodDef)member;
				moduleCtx.Name.MarkHelper(member, moduleCtx.Marker, (Protection)Parent);
			}
			ProtectionParameters.GetParameters(context, moduleCtx.InitMethod).Remove(Parent);

			var dataType = new TypeDefUser("", moduleCtx.Name.RandomName(), context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType"));
			dataType.Layout = TypeAttributes.ExplicitLayout;
			dataType.Visibility = TypeAttributes.NestedPrivate;
			dataType.IsSealed = true;
			moduleCtx.DataType = dataType;
			context.CurrentModule.GlobalType.NestedTypes.Add(dataType);
			moduleCtx.Name.MarkHelper(dataType, moduleCtx.Marker, (Protection)Parent);

			moduleCtx.DataField = new FieldDefUser(moduleCtx.Name.RandomName(), new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				Access = FieldAttributes.CompilerControlled
			};
			context.CurrentModule.GlobalType.Fields.Add(moduleCtx.DataField);
			moduleCtx.Name.MarkHelper(moduleCtx.DataField, moduleCtx.Marker, (Protection)Parent);

			MethodDef decoder = rt.GetRuntimeType("Confuser.Runtime.Constant").FindMethod("Get");
			moduleCtx.Decoders = new List<Tuple<MethodDef, DecoderDesc>>();
			for (int i = 0; i < moduleCtx.DecoderCount; i++) {
				MethodDef decoderInst = InjectHelper.Inject(decoder, context.CurrentModule);
				for (int j = 0; j < decoderInst.Body.Instructions.Count; j++) {
					Instruction instr = decoderInst.Body.Instructions[j];
					var method = instr.Operand as IMethod;
					var field = instr.Operand as IField;
					if (instr.OpCode == OpCodes.Call &&
					    method.DeclaringType.Name == "Mutation" &&
					    method.Name == "Value") {
						decoderInst.Body.Instructions[j] = Instruction.Create(OpCodes.Sizeof, new GenericMVar(0).ToTypeDefOrRef());
					}
					else if (instr.OpCode == OpCodes.Ldsfld &&
					         method.DeclaringType.Name == "Constant") {
						if (field.Name == "b") instr.Operand = moduleCtx.BufferField;
						else throw new UnreachableException();
					}
				}
				context.CurrentModule.GlobalType.Methods.Add(decoderInst);
				moduleCtx.Name.MarkHelper(decoderInst, moduleCtx.Marker, (Protection)Parent);
				ProtectionParameters.GetParameters(context, decoderInst).Remove(Parent);

				var decoderDesc = new DecoderDesc();

				decoderDesc.StringID = (byte)(moduleCtx.Random.NextByte() & 3);

				do decoderDesc.NumberID = (byte)(moduleCtx.Random.NextByte() & 3); while (decoderDesc.NumberID == decoderDesc.StringID);

				do decoderDesc.InitializerID = (byte)(moduleCtx.Random.NextByte() & 3); while (decoderDesc.InitializerID == decoderDesc.StringID || decoderDesc.InitializerID == decoderDesc.NumberID);

				MutationHelper.InjectKeys(decoderInst,
				                          new[] { 0, 1, 2 },
				                          new int[] { decoderDesc.StringID, decoderDesc.NumberID, decoderDesc.InitializerID });
				decoderDesc.Data = moduleCtx.ModeHandler.CreateDecoder(decoderInst, moduleCtx);
				moduleCtx.Decoders.Add(Tuple.Create(decoderInst, decoderDesc));
			}
		}

		void MutateInitializer(CEContext moduleCtx, MethodDef decomp) {
			moduleCtx.InitMethod.Body.SimplifyMacros(moduleCtx.InitMethod.Parameters);
			List<Instruction> instrs = moduleCtx.InitMethod.Body.Instructions.ToList();
			for (int i = 0; i < instrs.Count; i++) {
				Instruction instr = instrs[i];
				var method = instr.Operand as IMethod;
				if (instr.OpCode == OpCodes.Call) {
					if (method.DeclaringType.Name == "Mutation" &&
					    method.Name == "Crypt") {
						Instruction ldBlock = instrs[i - 2];
						Instruction ldKey = instrs[i - 1];
						Debug.Assert(ldBlock.OpCode == OpCodes.Ldloc && ldKey.OpCode == OpCodes.Ldloc);
						instrs.RemoveAt(i);
						instrs.RemoveAt(i - 1);
						instrs.RemoveAt(i - 2);
						instrs.InsertRange(i - 2, moduleCtx.ModeHandler.EmitDecrypt(moduleCtx.InitMethod, moduleCtx, (Local)ldBlock.Operand, (Local)ldKey.Operand));
					}
					else if (method.DeclaringType.Name == "Lzma" &&
					         method.Name == "Decompress") {
						instr.Operand = decomp;
					}
				}
			}
			moduleCtx.InitMethod.Body.Instructions.Clear();
			foreach (Instruction instr in instrs)
				moduleCtx.InitMethod.Body.Instructions.Add(instr);
		}
	}
}