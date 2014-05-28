using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer {
	internal class PostRenamePhase : ProtectionPhase {
		public PostRenamePhase(NameProtection parent)
			: base(parent) { }

		public override bool ProcessAll {
			get { return true; }
		}

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.AllDefinitions; }
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			var service = (NameService)context.Registry.GetService<INameService>();

			foreach (IRenamer renamer in service.Renamers) {
				foreach (IDnlibDef def in parameters.Targets)
					renamer.PostRename(context, service, def);
			}
		}
	}
}