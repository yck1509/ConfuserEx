using System;
using System.Linq;
using Confuser.Core;
using Confuser.Protections.AntiTamper;
using dnlib.DotNet;

namespace Confuser.Protections {
	public interface IAntiTamperService {
		void ExcludeMethod(ConfuserContext context, MethodDef method);
	}

	[BeforeProtection("Ki.ControlFlow"), AfterProtection("Ki.Constants")]
	internal class AntiTamperProtection : Protection, IAntiTamperService {
		public const string _Id = "anti tamper";
		public const string _FullId = "Ki.AntiTamper";
		public const string _ServiceId = "Ki.AntiTamper";
		static readonly object HandlerKey = new object();

		public override string Name {
			get { return "Anti Tamper Protection"; }
		}

		public override string Description {
			get { return "This protection ensures the integrity of application."; }
		}

		public override string Id {
			get { return _Id; }
		}

		public override string FullId {
			get { return _FullId; }
		}

		public override ProtectionPreset Preset {
			get { return ProtectionPreset.Maximum; }
		}

		protected override void Initialize(ConfuserContext context) {
			context.Registry.RegisterService(_ServiceId, typeof(IAntiTamperService), this);
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new InjectPhase(this));
			pipeline.InsertPreStage(PipelineStage.EndModule, new MDPhase(this));
		}

		public void ExcludeMethod(ConfuserContext context, MethodDef method) {
			ProtectionParameters.GetParameters(context, method).Remove(this);
		}

		class InjectPhase : ProtectionPhase {
			public InjectPhase(AntiTamperProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Methods; }
			}

			public override string Name {
				get { return "Anti-tamper helpers injection"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				if (!parameters.Targets.Any())
					return;

				Mode mode = parameters.GetParameter(context, context.CurrentModule, "mode", Mode.Normal);
				IModeHandler modeHandler;
				switch (mode) {
					case Mode.Normal:
						modeHandler = new NormalMode();
						break;
					case Mode.JIT:
						modeHandler = new JITMode();
						break;
					default:
						throw new UnreachableException();
				}
				modeHandler.HandleInject((AntiTamperProtection)Parent, context, parameters);
				context.Annotations.Set(context.CurrentModule, HandlerKey, modeHandler);
			}
		}

		class MDPhase : ProtectionPhase {
			public MDPhase(AntiTamperProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Methods; }
			}

			public override string Name {
				get { return "Anti-tamper metadata preparation"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				if (!parameters.Targets.Any())
					return;

				var modeHandler = context.Annotations.Get<IModeHandler>(context.CurrentModule, HandlerKey);
				modeHandler.HandleMD((AntiTamperProtection)Parent, context, parameters);
			}
		}

		enum Mode {
			Normal,
			JIT
		}
	}
}