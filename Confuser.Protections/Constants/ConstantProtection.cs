using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using Confuser.Protections.Constants;
using dnlib.DotNet;

namespace Confuser.Protections
{
    [BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.RefProxy")]
    class ConstantProtection : Protection
    {
        public const string _Id = "constants";
        public const string _FullId = "Ki.Constants";
        public const string _ServiceId = "Ki.Constants";

        protected override void Initialize(ConfuserContext context)
        {
           // context.Registry.RegisterService(_ServiceId, typeof(IControlFlowService), this);
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            pipeline.InsertPostStage(PipelineStage.BeginModule, new InjectPhase(this));
            pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new EncodePhase(this));
        }

        public override string Name
        {
            get { return "Constants Protection"; }
        }

        public override string Description
        {
            get { return "This protection encodes and compresses constants in the code."; }
        }

        public override string Id
        {
            get { return _Id; }
        }

        public override string FullId
        {
            get { return _FullId; }
        }

        public override ProtectionPreset Preset
        {
            get { return ProtectionPreset.Normal; }
        }

        internal static readonly object ContextKey = new object();

        public void ExcludeMethod(ConfuserContext context, MethodDef method)
        {
            ProtectionParameters.GetParameters(context, method).Remove(this);
        }
    }
}
