using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Renamer.Analyzers;
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

		void ParseParameters(IDnlibDef def, ConfuserContext context, NameService service, ProtectionParameters parameters) {
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
				context.CheckCancellation();
			}

			context.Logger.Debug("Analyzing...");
			RegisterRenamers(context, service);
			IList<IRenamer> renamers = service.Renamers;
			foreach (IDnlibDef def in parameters.Targets.WithProgress(context.Logger)) {
				Analyze(service, context, parameters, def, true);
				context.CheckCancellation();
			}
		}

		void RegisterRenamers(ConfuserContext context, NameService service) {
			bool wpf = false,
			     caliburn = false;

			foreach (var module in context.Modules)
				foreach (var asmRef in module.GetAssemblyRefs()) {
					if (asmRef.Name == "WindowsBase" || asmRef.Name == "PresentationCore" ||
					    asmRef.Name == "PresentationFramework" || asmRef.Name == "System.Xaml") {
						wpf = true;
					}
					else if (asmRef.Name == "Caliburn.Micro") {
						caliburn = true;
					}
				}

			if (wpf) {
				var wpfAnalyzer = new WPFAnalyzer();
				context.Logger.Debug("WPF found, enabling compatibility.");
				service.Renamers.Add(wpfAnalyzer);
				if (caliburn) {
					context.Logger.Debug("Caliburn.Micro found, enabling compatibility.");
					service.Renamers.Add(new CaliburnAnalyzer(wpfAnalyzer));
				}
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

			if (!runAnalyzer || parameters.GetParameter(context, def, "forceRen", false))
				return;

			foreach (IRenamer renamer in service.Renamers)
				renamer.Analyze(context, service, def);
		}

		void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, TypeDef type) {
			if (type.IsVisibleOutside() && !parameters.GetParameter(context, type, "renPublic", false)) {
				service.SetCanRename(type, false);
			}
			else if (type.IsRuntimeSpecialName || type.IsSpecialName) {
				service.SetCanRename(type, false);
			}
			else if (type.FullName == "ConfusedByAttribute") {
				// Courtesy
				service.SetCanRename(type, false);
			}

			if (parameters.GetParameter(context, type, "forceRen", false))
				return;

			if (type.InheritsFromCorlib("System.Attribute")) {
				service.ReduceRenameMode(type, RenameMode.ASCII);
			}
		}

		void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, MethodDef method) {
			if (method.DeclaringType.IsVisibleOutside() &&
			    (method.IsFamily || method.IsFamilyOrAssembly || method.IsPublic) &&
			    !parameters.GetParameter(context, method, "renPublic", false))
				service.SetCanRename(method, false);

			else if (method.IsRuntimeSpecialName || method.IsSpecialName)
				service.SetCanRename(method, false);

			else if (parameters.GetParameter(context, method, "forceRen", false))
				return;

			else if (method.DeclaringType.IsComImport() && !method.HasAttribute("System.Runtime.InteropServices.DispIdAttribute"))
				service.SetCanRename(method, false);

			else if (method.DeclaringType.IsDelegate())
				service.SetCanRename(method, false);
		}

		void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, FieldDef field) {
			if (field.DeclaringType.IsVisibleOutside() &&
			    (field.IsFamily || field.IsFamilyOrAssembly || field.IsPublic) &&
			    !parameters.GetParameter(context, field, "renPublic", false))
				service.SetCanRename(field, false);

			else if (field.IsRuntimeSpecialName || field.IsSpecialName)
				service.SetCanRename(field, false);

			else if (field.DeclaringType.IsSerializable && !field.IsNotSerialized)
				service.SetCanRename(field, false);

			else if (field.IsLiteral)
				service.SetCanRename(field, false);
		}

		void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, PropertyDef property) {
			if (property.DeclaringType.IsVisibleOutside() &&
			    !parameters.GetParameter(context, property, "renPublic", false))
				service.SetCanRename(property, false);

			else if (property.IsRuntimeSpecialName || property.IsSpecialName)
				service.SetCanRename(property, false);

			else if (parameters.GetParameter(context, property, "forceRen", false))
				return;

			else if (property.DeclaringType.Implements("System.ComponentModel.INotifyPropertyChanged"))
				service.SetCanRename(property, false);

			else if (property.DeclaringType.Name.String.Contains("AnonymousType"))
				service.SetCanRename(property, false);
		}

		void Analyze(NameService service, ConfuserContext context, ProtectionParameters parameters, EventDef evt) {
			if (evt.DeclaringType.IsVisibleOutside() &&
			    !parameters.GetParameter(context, evt, "renPublic", false))
				service.SetCanRename(evt, false);

			else if (evt.IsRuntimeSpecialName || evt.IsSpecialName)
				service.SetCanRename(evt, false);
		}
	}
}