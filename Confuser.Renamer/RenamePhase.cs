using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer {
	internal class RenamePhase : ProtectionPhase {
		public RenamePhase(NameProtection parent)
			: base(parent) { }

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.AllDefinitions; }
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			var service = (NameService) context.Registry.GetService<INameService>();

			context.Logger.Debug("Renaming...");
			foreach (IRenamer renamer in service.Renamers) {
				foreach (IDnlibDef def in parameters.Targets)
					renamer.PreRename(context, service, def);
			}

			foreach (IDnlibDef def in parameters.Targets) {
				if (def is MethodDef)
					if (parameters.GetParameter(context, def, "renameArgs", true)) {
						foreach (ParamDef param in ((MethodDef) def).ParamDefs)
							param.Name = null;
					}

				if (!service.CanRename(def))
					continue;

				RenameMode mode = service.GetRenameMode(def);

				IList<INameReference> references = service.GetReferences(def);
				bool cancel = false;
				foreach (INameReference refer in references) {
					cancel |= refer.ShouldCancelRename();
					if (cancel) break;
				}
				if (cancel)
					continue;

				if (def is TypeDef) {
					var typeDef = (TypeDef) def;
					if (parameters.GetParameter(context, def, "flatten", true)) {
						typeDef.Namespace = "";
						typeDef.Name = service.ObfuscateName(typeDef.FullName, mode);
					}
					else {
						typeDef.Namespace = service.ObfuscateName(typeDef.Namespace, mode);
						typeDef.Name = service.ObfuscateName(typeDef.Name, mode);
					}
				}
				else
					def.Name = service.ObfuscateName(def.Name, mode);

				foreach (INameReference refer in references.ToList()) {
					if (!refer.UpdateNameReference(context, service)) {
						context.Logger.ErrorFormat("Failed to update name reference on '{0}'.", def);
						throw new ConfuserException(null);
					}
				}
			}
		}
	}
}