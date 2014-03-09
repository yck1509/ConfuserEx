using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer;
using dnlib.DotNet.Writer;
using System.Diagnostics;

namespace Confuser.Protections.ControlFlow
{
    class ControlFlowPhase : ProtectionPhase
    {
        public ControlFlowPhase(ControlFlowProtection parent)
            : base(parent)
        {
        }

        public override ProtectionTargets Targets
        {
            get { return ProtectionTargets.Methods; }
        }

        static CFContext ParseParameters(MethodDef method, ConfuserContext context, ProtectionParameters parameters, RandomGenerator random, bool disableOpti)
        {
            CFContext ret = new CFContext();
            ret.Type = parameters.GetParameter<CFType>(context, method, "type", CFType.Switch);
            ret.Predicate = parameters.GetParameter<PredicateType>(context, method, "predicate", PredicateType.Normal);

            int rawIntensity = parameters.GetParameter<int>(context, method, "intensity", 60);
            ret.Intensity = rawIntensity / 100.0;
            ret.Depth = parameters.GetParameter<int>(context, method, "depth", 4);

            ret.JunkCode = parameters.GetParameter<bool>(context, method, "junk", false) && !disableOpti;

            ret.Random = random;
            ret.Method = method;
            ret.Context = context;
            ret.DynCipher = context.Registry.GetService<IDynCipherService>();

            return ret;
        }

        static bool DisabledOptimization(ModuleDef module)
        {
            bool disableOpti = false;
            var debugAttr = module.Assembly.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
            if (debugAttr != null)
            {
                if (debugAttr.ConstructorArguments.Count == 1)
                    disableOpti |= ((DebuggableAttribute.DebuggingModes)(int)debugAttr.ConstructorArguments[0].Value & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
                else
                    disableOpti |= (bool)debugAttr.ConstructorArguments[1].Value;
            }
            debugAttr = module.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");
            if (debugAttr != null)
            {
                if (debugAttr.ConstructorArguments.Count == 1)
                    disableOpti |= ((DebuggableAttribute.DebuggingModes)(int)debugAttr.ConstructorArguments[0].Value & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0;
                else
                    disableOpti |= (bool)debugAttr.ConstructorArguments[1].Value;
            }
            return disableOpti;
        }

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            bool disabledOpti = DisabledOptimization(context.CurrentModule);
            var random = context.Registry.GetService<IRandomService>().GetRandomGenerator(ControlFlowProtection._FullId);

            foreach (var method in parameters.Targets.OfType<MethodDef>())
                if (method.HasBody && method.Body.Instructions.Count > 0)
                {
                    ProcessMethod(method.Body, ParseParameters(method, context, parameters, random, disabledOpti));
                }
        }

        static readonly JumpMangler Jump = new JumpMangler();
        static readonly SwitchMangler Switch = new SwitchMangler();

        static ManglerBase GetMangler(CFType type)
        {
            if (type == CFType.Switch)
                return Switch;
            else
                return Jump;
        }

        void ProcessMethod(CilBody body, CFContext ctx)
        {
            uint maxStack;
            if (!MaxStackCalculator.GetMaxStack(body.Instructions, body.ExceptionHandlers, out maxStack))
            {
                ctx.Context.Logger.Error("Failed to calcuate maxstack.");
                throw new ConfuserException(null);
            }
            body.MaxStack = (ushort)maxStack;
            ScopeBlock root = BlockParser.ParseBody(body);

            GetMangler(ctx.Type).Mangle(body, root, ctx);

            body.Instructions.Clear();
            root.ToBody(body);
            foreach (var eh in body.ExceptionHandlers)
            {
                eh.TryEnd = body.Instructions[body.Instructions.IndexOf(eh.TryEnd) + 1];
                eh.HandlerEnd = body.Instructions[body.Instructions.IndexOf(eh.HandlerEnd) + 1];
            }
            body.KeepOldMaxStack = true;
        }
    }
}
