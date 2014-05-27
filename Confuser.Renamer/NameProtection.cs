using Confuser.Core;

namespace Confuser.Renamer {
	internal class NameProtection : Protection {
		public const string _Id = "rename";
		public const string _FullId = "Ki.Rename";
		public const string _ServiceId = "Ki.Rename";

		public override string Name {
			get { return "Name Protection"; }
		}

		public override string Description {
			get { return "This protection obfuscate the symbols' name so the decompiled source code can neither be compiled nor read."; }
		}

		public override string Id {
			get { return _Id; }
		}

		public override string FullId {
			get { return _FullId; }
		}

		public override ProtectionPreset Preset {
			get { return ProtectionPreset.Minimum; }
		}

		protected override void Initialize(ConfuserContext context) {
			context.Registry.RegisterService(_ServiceId, typeof (INameService), new NameService(context));
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.Inspection, new AnalyzePhase(this));
			pipeline.InsertPreStage(PipelineStage.EndModule, new RenamePhase(this));
			pipeline.InsertPostStage(PipelineStage.EndModule, new PostRenamePhase(this));
		}
	}
}