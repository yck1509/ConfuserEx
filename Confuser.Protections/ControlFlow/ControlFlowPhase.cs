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

        static CFContext ParseParameters(MethodDef method, ConfuserContext context, ProtectionParameters parameters, RandomGenerator random)
        {
            CFContext ret = new CFContext();
            ret.Type = parameters.GetParameter<CFType>(context, method, "type", CFType.Switch);
            ret.Predicate = parameters.GetParameter<PredicateType>(context, method, "predicate", PredicateType.Normal);

            int rawIntensity = parameters.GetParameter<int>(context, method, "intensity", 60);
            ret.Intensity = rawIntensity / 100.0;
            ret.Depth = parameters.GetParameter<int>(context, method, "depth", 5);

            ret.JunkCode = parameters.GetParameter<bool>(context, method, "junk", false);

            ret.Random = random;
            ret.Method = method;
            ret.Context = context;
            ret.DynCipher = context.Registry.GetService<IDynCipherService>();

            return ret;
        }

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            var random = context.Registry.GetService<IRandomService>().GetRandomGenerator(ControlFlowProtection._FullId);
            foreach (var method in parameters.Targets.OfType<MethodDef>())
                if (method.HasBody && method.Body.Instructions.Count > 0)
                {
                    ProcessMethod(method.Body, ParseParameters(method, context, parameters, random));
                }

            context.CurrentModuleWriterOptions.MetaDataOptions.Flags |= dnlib.DotNet.Writer.MetaDataFlags.KeepOldMaxStack;
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
            ScopeBlock root = BlockParser.ParseBody(body);

            GetMangler(ctx.Type).Mangle(body, root, ctx);

            body.Instructions.Clear();
            root.ToBody(body);
            foreach (var eh in body.ExceptionHandlers)
            {
                eh.TryEnd = body.Instructions[body.Instructions.IndexOf(eh.TryEnd) + 1];
                eh.HandlerEnd = body.Instructions[body.Instructions.IndexOf(eh.HandlerEnd) + 1];
            }
        }
    }
}
