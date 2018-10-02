using Confuser.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble {
    class TypeScrambleProtection : Protection {
        public override ProtectionPreset Preset => ProtectionPreset.None;

        public override string Name => "Type Scrambler";

        public override string Description => "Replaces types with generics";

        public override string Id => "typescramble";

        public override string FullId => "BahNahNah.typescramble";

        protected override void Initialize(ConfuserContext context) {
            context.Registry.RegisterService(FullId, typeof(TypeService), new TypeService(context));
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline) {
            pipeline.InsertPreStage(PipelineStage.Inspection, new AnalyzePhase(this));
            pipeline.InsertPostStage(PipelineStage.Inspection, new ScramblePhase(this));
        }
    }
}
