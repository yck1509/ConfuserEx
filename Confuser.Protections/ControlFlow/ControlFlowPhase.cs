using System;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.ControlFlow {
	internal class ControlFlowPhase : ProtectionPhase {
		private static readonly JumpMangler Jump = new JumpMangler();
		private static readonly SwitchMangler Switch = new SwitchMangler();

		public ControlFlowPhase(ControlFlowProtection parent)
			: base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.Methods; }
		}

		public override string Name {
			get { return "Control flow mangling"; }
		}

		private static CFContext ParseParameters(MethodDef method, ConfuserContext context, ProtectionParameters parameters, RandomGenerator random, bool disableOpti) {
			var ret = new CFContext();
			ret.Type = parameters.GetParameter(context, method, "type", CFType.Switch);
			ret.Predicate = parameters.GetParameter(context, method, "predicate", PredicateType.Normal);

			int rawIntensity = parameters.GetParameter(context, method, "intensity", 60);
			ret.Intensity = rawIntensity / 100.0;
			ret.Depth = parameters.GetParameter(context, method, "depth", 4);

			ret.JunkCode = parameters.GetParameter(context, method, "junk", false) && !disableOpti;

			ret.Random = random;
			ret.Method = method;
			ret.Context = context;
			ret.DynCipher = context.Registry.GetService<IDynCipherService>();

			if (ret.Predicate == PredicateType.x86) {
				if ((context.CurrentModule.Cor20HeaderFlags & ComImageFlags.ILOnly) != 0)
					context.CurrentModuleWriterOptions.Cor20HeaderOptions.Flags &= ~ComImageFlags.ILOnly;
			}

			return ret;
		}

		private static bool DisabledOptimization(ModuleDef module) {
			bool disableOpti = false;
			CustomAttribute debugAttr = module.Assembly.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
			if (debugAttr != null) {
				if (debugAttr.ConstructorArguments.Count == 1)
					disableOpti |= ((DebuggableAttribute.DebuggingModes)(int)debugAttr.ConstructorArguments[0].Value & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
				else
					disableOpti |= (bool)debugAttr.ConstructorArguments[1].Value;
			}
			debugAttr = module.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
			if (debugAttr != null) {
				if (debugAttr.ConstructorArguments.Count == 1)
					disableOpti |= ((DebuggableAttribute.DebuggingModes)(int)debugAttr.ConstructorArguments[0].Value & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
				else
					disableOpti |= (bool)debugAttr.ConstructorArguments[1].Value;
			}
			return disableOpti;
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			bool disabledOpti = DisabledOptimization(context.CurrentModule);
			RandomGenerator random = context.Registry.GetService<IRandomService>().GetRandomGenerator(ControlFlowProtection._FullId);

			foreach (MethodDef method in parameters.Targets.OfType<MethodDef>().WithProgress(context.Logger))
				if (method.HasBody && method.Body.Instructions.Count > 0) {
					ProcessMethod(method.Body, ParseParameters(method, context, parameters, random, disabledOpti));
					context.CheckCancellation();
				}
		}

		private static ManglerBase GetMangler(CFType type) {
			if (type == CFType.Switch)
				return Switch;
			return Jump;
		}

		private void ProcessMethod(CilBody body, CFContext ctx) {
			uint maxStack;
			if (!MaxStackCalculator.GetMaxStack(body.Instructions, body.ExceptionHandlers, out maxStack)) {
				ctx.Context.Logger.Error("Failed to calcuate maxstack.");
				throw new ConfuserException(null);
			}
			body.MaxStack = (ushort)maxStack;
			ScopeBlock root = BlockParser.ParseBody(body);

			GetMangler(ctx.Type).Mangle(body, root, ctx);

			body.Instructions.Clear();
			root.ToBody(body);
			foreach (ExceptionHandler eh in body.ExceptionHandlers) {
				eh.TryEnd = body.Instructions[body.Instructions.IndexOf(eh.TryEnd) + 1];
				eh.HandlerEnd = body.Instructions[body.Instructions.IndexOf(eh.HandlerEnd) + 1];
			}
			body.KeepOldMaxStack = true;
		}
	}
}