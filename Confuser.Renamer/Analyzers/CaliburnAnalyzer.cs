using System;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.Analyzers {
	internal class CaliburnAnalyzer : IRenamer {
		public void Analyze(ConfuserContext context, INameService service, IDnlibDef def) {
			var type = def as TypeDef;
			if (type == null || type.DeclaringType != null)
				return;
			if (type.Name.Contains("ViewModel")) {
				string viewNs = type.Namespace.Replace("ViewModels", "Views");
				string viewName = type.Name.Replace("PageViewModel", "Page").Replace("ViewModel", "View");
				TypeDef view = type.Module.Find(viewNs + "." + viewName, true);
				if (view != null) {
					service.SetCanRename(type, false);
					service.SetCanRename(view, false);
				}
			}
		}

		public void PreRename(ConfuserContext context, INameService service, IDnlibDef def) {
			//
		}

		public void PostRename(ConfuserContext context, INameService service, IDnlibDef def) {
			//
		}
	}
}
