using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Resources {
	internal class InjectPhase : ProtectionPhase {
		public InjectPhase(ResourceProtection parent)
			: base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Methods; }
		}

		public override string Name {
			get { return "Resource encryption helpers injection"; }
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			if (parameters.Targets.Any()) {
				if (!UTF8String.IsNullOrEmpty(context.CurrentModule.Assembly.Culture)) {
					context.Logger.DebugFormat("Skipping resource encryption for satellite assembly '{0}'.",
					                           context.CurrentModule.Assembly.FullName);
					return;
				}
				var compression = context.Registry.GetService<ICompressionService>();
				var name = context.Registry.GetService<INameService>();
				var marker = context.Registry.GetService<IMarkerService>();
				var rt = context.Registry.GetService<IRuntimeService>();
				var moduleCtx = new REContext {
					Random = context.Registry.GetService<IRandomService>().GetRandomGenerator(Parent.Id),
					Context = context,
					Module = context.CurrentModule,
					Marker = marker,
					DynCipher = context.Registry.GetService<IDynCipherService>(),
					Name = name
				};

				// Extract parameters
				moduleCtx.Mode = parameters.GetParameter(context, context.CurrentModule, "mode", Mode.Normal);

				switch (moduleCtx.Mode) {
					case Mode.Normal:
						moduleCtx.ModeHandler = new NormalMode();
						break;
					case Mode.Dynamic:
						moduleCtx.ModeHandler = new DynamicMode();
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

				new MDPhase(moduleCtx).Hook();
			}
		}

		void InjectHelpers(ConfuserContext context, ICompressionService compression, IRuntimeService rt, REContext moduleCtx) {
			var rtName = context.Packer != null ? "Confuser.Runtime.Resource_Packer" : "Confuser.Runtime.Resource";
			IEnumerable<IDnlibDef> members = InjectHelper.Inject(rt.GetRuntimeType(rtName), context.CurrentModule.GlobalType, context.CurrentModule);
			foreach (IDnlibDef member in members) {
				if (member.Name == "Initialize")
					moduleCtx.InitMethod = (MethodDef)member;
				moduleCtx.Name.MarkHelper(member, moduleCtx.Marker, (Protection)Parent);
			}

			var dataType = new TypeDefUser("", moduleCtx.Name.RandomName(), context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType"));
			dataType.Layout = TypeAttributes.ExplicitLayout;
			dataType.Visibility = TypeAttributes.NestedPrivate;
			dataType.IsSealed = true;
			dataType.ClassLayout = new ClassLayoutUser(1, 0);
			moduleCtx.DataType = dataType;
			context.CurrentModule.GlobalType.NestedTypes.Add(dataType);
			moduleCtx.Name.MarkHelper(dataType, moduleCtx.Marker, (Protection)Parent);

			moduleCtx.DataField = new FieldDefUser(moduleCtx.Name.RandomName(), new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				HasFieldRVA = true,
				InitialValue = new byte[0],
				Access = FieldAttributes.CompilerControlled
			};
			context.CurrentModule.GlobalType.Fields.Add(moduleCtx.DataField);
			moduleCtx.Name.MarkHelper(moduleCtx.DataField, moduleCtx.Marker, (Protection)Parent);
		}

		void MutateInitializer(REContext moduleCtx, MethodDef decomp) {
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

			MutationHelper.ReplacePlaceholder(moduleCtx.InitMethod, arg => {
				var repl = new List<Instruction>();
				repl.AddRange(arg);
				repl.Add(Instruction.Create(OpCodes.Dup));
				repl.Add(Instruction.Create(OpCodes.Ldtoken, moduleCtx.DataField));
				repl.Add(Instruction.Create(OpCodes.Call, moduleCtx.Module.Import(
					typeof(RuntimeHelpers).GetMethod("InitializeArray"))));
				return repl.ToArray();
			});
			moduleCtx.Context.Registry.GetService<IConstantService>().ExcludeMethod(moduleCtx.Context, moduleCtx.InitMethod);
		}
	}
}