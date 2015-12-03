using System;
using Confuser.Core;
using Confuser.Protections.ReferenceProxy;
using dnlib.DotNet;

namespace Confuser.Protections {
	public interface IReferenceProxyService {
		void ExcludeMethod(ConfuserContext context, MethodDef method);
		void ExcludeTarget(ConfuserContext context, MethodDef method);
		bool IsTargeted(ConfuserContext context, MethodDef method);
	}

	[AfterProtection("Ki.AntiDebug", "Ki.AntiDump")]
	[BeforeProtection("Ki.ControlFlow")]
	internal class ReferenceProxyProtection : Protection, IReferenceProxyService {
		public const string _Id = "ref proxy";
		public const string _FullId = "Ki.RefProxy";
		public const string _ServiceId = "Ki.RefProxy";

		internal static object TargetExcluded = new object();
		internal static object Targeted = new object();

		public override string Name {
			get { return "Reference Proxy Protection"; }
		}

		public override string Description {
			get { return "This protection encodes and hides references to type/method/fields."; }
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

		public void ExcludeMethod(ConfuserContext context, MethodDef method) {
			ProtectionParameters.GetParameters(context, method).Remove(this);
		}

		public void ExcludeTarget(ConfuserContext context, MethodDef method) {
			context.Annotations.Set(method, TargetExcluded, TargetExcluded);
		}

		public bool IsTargeted(ConfuserContext context, MethodDef method) {
			return context.Annotations.Get<object>(method, Targeted) != null;
		}

		protected override void Initialize(ConfuserContext context) {
			context.Registry.RegisterService(_ServiceId, typeof(IReferenceProxyService), this);
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new ReferenceProxyPhase(this));
		}
	}
}