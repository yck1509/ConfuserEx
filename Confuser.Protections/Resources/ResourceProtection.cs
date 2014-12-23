using System;
using Confuser.Core;
using Confuser.Protections.Resources;

namespace Confuser.Protections {
	[BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.Constants")]
	internal class ResourceProtection : Protection {
		public const string _Id = "resources";
		public const string _FullId = "Ki.Resources";
		public const string _ServiceId = "Ki.Resources";

		public override string Name {
			get { return "Resources Protection"; }
		}

		public override string Description {
			get { return "This protection encodes and compresses the embedded resources."; }
		}

		public override string Id {
			get { return _Id; }
		}

		public override string FullId {
			get { return _FullId; }
		}

		public override ProtectionPreset Preset {
			get { return ProtectionPreset.Normal; }
		}

		protected override void Initialize(ConfuserContext context) { }

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new InjectPhase(this));
		}
	}
}