using System;
using Confuser.Core;

namespace Confuser.DynCipher {
	internal class DynCipherComponent : ConfuserComponent {
		public const string _ServiceId = "Confuser.DynCipher";

		public override string Name {
			get { return "Dynamic Cipher"; }
		}

		public override string Description {
			get { return "Provides dynamic cipher generation services."; }
		}

		public override string Id {
			get { return _ServiceId; }
		}

		public override string FullId {
			get { return _ServiceId; }
		}

		protected override void Initialize(ConfuserContext context) {
			context.Registry.RegisterService(_ServiceId, typeof(IDynCipherService), new DynCipherService());
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			//
		}
	}
}