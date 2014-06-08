using System.Collections.Generic;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer {
	internal class AnalyzePhase : ProtectionPhase {
		public AnalyzePhase(NameProtection parent)
			: base(parent) { }

		public override bool ProcessAll {
			get { return true; }
		}

		public override ProtectionTargets Targets {
			get { return ProtectionTargets.AllDefinitions; }
		}

		private void ParseParameters(IDnlibDef def, ConfuserContext context, NameService service, ProtectionParameters parameters) {
			var mode = parameters.GetParameter<RenameMode?>(context, def, "mode", null);
			if (mode != null)
				service.SetRenameMode(def, mode.Value);
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			var service = (NameService)context.Registry.GetService<INameService>();
			context.Logger.Debug("Building VTables & identifier list...");
			foreach (IDnlibDef def in parameters.Targets) {
				ParseParameters(def, context, service, parameters);

				if (def is ModuleDef) {
					var module = (ModuleDef)def;
					foreach (Resource res in module.Resources)
						service.SetOriginalName(res, res.Name);
				}
				else
					service.SetOriginalName(def, def.Name);

				if (def is TypeDef) {
					service.GetVTables().GetVTable((TypeDef)def);
					service.SetOriginalNamespace(def, ((TypeDef)def).Namespace);
				}
			}

			context.Logger.Debug("Analyzing...");
			IList<IRenamer> renamers = service.Renamers;
			foreach (IDnlibDef def in parameters.Targets) {
				Analyze(service, context, parameters, def, true);
			}
		}

		internal void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, IDnlibDef def, bool runAnalyzer) {
			if (def is TypeDef)
				Analyze(service, context, parameters, (TypeDef)def);
			else if (def is MethodDef)
				Analyze(service, context, parameters, (MethodDef)def);
			else if (def is FieldDef)
				Analyze(service, context, parameters, (FieldDef)def);
			else if (def is PropertyDef)
				Analyze(service, context, parameters, (PropertyDef)def);
			else if (def is EventDef)
				Analyze(service, context, parameters, (EventDef)def);
			else if (def is ModuleDef)
				service.SetCanRename(def, false);

			if (!runAnalyzer)
				return;

			foreach (IRenamer renamer in service.Renamers)
				renamer.Analyze(context, service, def);
		}

		private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, TypeDef type) {
			if (type.IsVisibleOutside() && !parameters.GetParameter<bool>(context, type, "renPublic", false)) {
				service.SetCanRename(type, false);
			}
			else if (type.IsRuntimeSpecialName || type.IsSpecialName) {
				service.SetCanRename(type, false);
			}
			else if (type.FullName == "ConfusedByAttribute") {
				// Courtesy
				service.SetCanRename(type, false);
			}

			if (type.InheritsFromCorlib("System.Attribute")) {
				service.ReduceRenameMode(type, RenameMode.ASCII);
			}
		}

		private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, MethodDef method) {
			if (method.DeclaringType.IsVisibleOutside() &&
				(method.IsFamily || method.IsFamilyOrAssembly || method.IsPublic) &&
				!parameters.GetParameter<bool>(context, method, "renPublic", false))
				service.SetCanRename(method, false);

			else if (method.IsRuntimeSpecialName || method.IsSpecialName)
				service.SetCanRename(method, false);

			else if (method.DeclaringType.IsComImport() && !method.HasAttribute("System.Runtime.InteropServices.DispIdAttribute"))
				service.SetCanRename(method, false);

			else if (method.DeclaringType.IsDelegate())
				service.SetCanRename(method, false);
		}

		private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, FieldDef field) {
			if (field.DeclaringType.IsVisibleOutside() &&
				(field.IsFamily || field.IsFamilyOrAssembly || field.IsPublic) &&
				!parameters.GetParameter<bool>(context, field, "renPublic", false))
				service.SetCanRename(field, false);

			else if (field.IsRuntimeSpecialName || field.IsSpecialName)
				service.SetCanRename(field, false);
		}

		private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, PropertyDef property) {
			if (property.DeclaringType.IsVisibleOutside() &&
				!parameters.GetParameter<bool>(context, property, "renPublic", false))
				service.SetCanRename(property, false);

			else if (property.IsRuntimeSpecialName || property.IsSpecialName)
				service.SetCanRename(property, false);

			else if (property.DeclaringType.Implements("System.ComponentModel.INotifyPropertyChanged"))
				service.SetCanRename(property, false);

			else if (property.DeclaringType.Name.String.Contains("AnonymousType"))
				service.SetCanRename(property, false);
		}

		private void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, EventDef evt) {
			if (evt.DeclaringType.IsVisibleOutside() &&
				!parameters.GetParameter<bool>(context, evt, "renPublic", false))
				service.SetCanRename(evt, false);

			else if (evt.IsRuntimeSpecialName || evt.IsSpecialName)
				service.SetCanRename(evt, false);
		}
	}
}