using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Confuser.Core.Services;

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

        CFContext ParseParameter(
            MethodDef method, ConfuserContext context, 
            ProtectionParameters parameters, RandomGenerator random)
        {
            CFContext ret = new CFContext();
            ret.Type = parameters.GetParameter<CFType>(context, method, "type", CFType.Switch);

            int rawIntensity = parameters.GetParameter<int>(context, method, "intensity", 60);
            ret.Intensity = rawIntensity / 100.0;

            ret.JunkCode = parameters.GetParameter<bool>(context, method, "junk", false);
            ret.FakeBranch = parameters.GetParameter<bool>(context, method, "fakeBr", false);
            ret.Random = random;
            ret.Method = method;

            return ret;
        }

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            var random = context.Registry.GetService<IRandomService>().GetRandomGenerator(ControlFlowProtection._FullId);
            foreach (var method in parameters.Targets.OfType<MethodDef>())
                if (method.HasBody && method.Body.Instructions.Count > 0)
                {
                    ProcessMethod(method.Body, ParseParameter(method, context, parameters, random));
                }

            context.CurrentModuleWriterOptions.MetaDataOptions.Flags |= dnlib.DotNet.Writer.MetaDataFlags.KeepOldMaxStack;
        }

        void ProcessMethod(CilBody body, CFContext ctx)
        {
            ScopeBlock root = BlockParser.ParseBody(body);

            JumpMangler.Mangle(body, root, ctx);

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
