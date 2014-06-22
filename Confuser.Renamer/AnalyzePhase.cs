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

		public override string Name {
			get { return "Name analysis"; }
		}

		private void ParseParameters(IDnlibDef def, ConfuserContext context, NameService service, ProtectionParameters parameters) {
			var mode = parameters.GetParameter<RenameMode?>(context, def, "mode", null);
			if (mode != null)
				service.SetRenameMode(def, mode.Value);
		}

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			var service = (NameService)context.Registry.GetService<INameService>();
			context.Logger.Debug("Building VTables & identifier list...");
			foreach (IDnlibDef def in parameters.Targets.WithProgress(context.Logger)) {
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
			foreach (IDnlibDef def in parameters.Targets.WithProgress(context.Logger)) {
				Analyze(service, context, def, true);
			}
		}

		internal void Analyze(NameService service, ConfuserContext context, IDnlibDef def, bool runAnalyzer) {
			if (def is TypeDef)
				Analyze(service, context, (TypeDef)def);
			else if (def is MethodDef)
				Analyze(service, context, (MethodDef)def);
			else if (def is FieldDef)
				Analyze(service, context, (FieldDef)def);
			else if (def is PropertyDef)
				Analyze(service, context, (PropertyDef)def);
			else if (def is EventDef)
				Analyze(service, context, (EventDef)def);
			else if (def is ModuleDef)
				service.SetCanRename(def, false);

			if (!runAnalyzer)
				return;

			foreach (IRenamer renamer in service.Renamers)
				renamer.Analyze(context, service, def);
		}

		private void Analyze(NameService service, ConfuserContext context, TypeDef type) {
			if (type.IsVisibleOutside()) {
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

		private void Analyze(NameService service, ConfuserContext context, MethodDef method) {
			if (method.DeclaringType.IsVisibleOutside() &&
			    (method.IsFamily || method.IsFamilyOrAssembly || method.IsPublic))
				service.SetCanRename(method, false);

			else if (method.IsRuntimeSpecialName || method.IsSpecialName)
				service.SetCanRename(method, false);

			else if (method.DeclaringType.IsComImport() && !method.HasAttribute("System.Runtime.InteropServices.DispIdAttribute"))
				service.SetCanRename(method, false);

			else if (method.DeclaringType.IsDelegate())
				service.SetCanRename(method, false);
		}

		private void Analyze(NameService service, ConfuserContext context, FieldDef field) {
			if (field.DeclaringType.IsVisibleOutside() &&
			    (field.IsFamily || field.IsFamilyOrAssembly || field.IsPublic))
				service.SetCanRename(field, false);

			else if (field.IsRuntimeSpecialName || field.IsSpecialName)
				service.SetCanRename(field, false);
		}

		private void Analyze(NameService service, ConfuserContext context, PropertyDef property) {
			if (property.DeclaringType.IsVisibleOutside())
				service.SetCanRename(property, false);

			else if (property.IsRuntimeSpecialName || property.IsSpecialName)
				service.SetCanRename(property, false);

			else if (property.DeclaringType.Implements("System.ComponentModel.INotifyPropertyChanged"))
				service.SetCanRename(property, false);

			else if (property.DeclaringType.Name.String.Contains("AnonymousType"))
				service.SetCanRename(property, false);
		}

		private void Analyze(NameService service, ConfuserContext context, EventDef evt) {
			if (evt.DeclaringType.IsVisibleOutside())
				service.SetCanRename(evt, false);

			else if (evt.IsRuntimeSpecialName || evt.IsSpecialName)
				service.SetCanRename(evt, false);
		}
	}
}