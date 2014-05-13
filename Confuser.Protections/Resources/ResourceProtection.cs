using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Protections.Resources;

namespace Confuser.Protections
{
    [BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.Constants")]
    class ResourceProtection : Protection
    {
        public const string _Id = "resources";
        public const string _FullId = "Ki.Resources";
        public const string _ServiceId = "Ki.Resources";

        protected override void Initialize(ConfuserContext context)
        {
            // context.Registry.RegisterService(_ServiceId, typeof(IControlFlowService), this);
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            pipeline.InsertPostStage(PipelineStage.BeginModule, new InjectPhase(this));
        }

        public override string Name
        {
            get { return "Resources Protection"; }
        }

        public override string Description
        {
            get { return "This protection encodes and compresses the embedded resources."; }
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
    }
}
