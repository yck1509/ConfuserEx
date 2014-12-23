using System;
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

		public override string Name {
			get { return "Renaming"; }
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			var service = (NameService)context.Registry.GetService<INameService>();

			context.Logger.Debug("Renaming...");
			foreach (IRenamer renamer in service.Renamers) {
				foreach (IDnlibDef def in parameters.Targets)
					renamer.PreRename(context, service, def);
				context.CheckCancellation();
			}

			foreach (IDnlibDef def in parameters.Targets.WithProgress(context.Logger)) {
				if (def is ModuleDef && parameters.GetParameter(context, def, "rickroll", true))
					RickRoller.CommenceRickroll(context, (ModuleDef)def);

				bool canRename = service.CanRename(def);
				if (def is MethodDef)
					if (canRename && parameters.GetParameter(context, def, "renameArgs", true)) {
						foreach (ParamDef param in ((MethodDef)def).ParamDefs)
							param.Name = null;
					}

				if (!canRename)
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
					var typeDef = (TypeDef)def;
					if (parameters.GetParameter(context, def, "flatten", true)) {
						typeDef.Name = service.ObfuscateName(typeDef.FullName, mode);
						typeDef.Namespace = "";
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
				context.CheckCancellation();
			}
		}
	}
}