using System;
using System.Text.RegularExpressions;
using Confuser.Core;
using Confuser.Renamer.BAML;
using dnlib.DotNet;

namespace Confuser.Renamer.Analyzers {
	internal class CaliburnAnalyzer : IRenamer {

		public CaliburnAnalyzer(WPFAnalyzer wpfAnalyzer) {
			wpfAnalyzer.AnalyzeBAMLElement += AnalyzeBAMLElement;
		}

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

				// Test for Multi-view
				string multiViewNs = type.Namespace + "." + type.Name.Replace("ViewModel", "");
				foreach (var t in type.Module.Types)
					if (t.Namespace == multiViewNs) {
						service.SetCanRename(type, false);
						service.SetCanRename(t, false);
					}
			}
		}

		private void AnalyzeBAMLElement(BAMLAnalyzer analyzer, BamlElement elem) {
			foreach (var rec in elem.Body) {
				var prop = rec as PropertyWithConverterRecord;
				if (prop == null)
					continue;
				var attr = analyzer.ResolveAttribute(prop.AttributeId);
				if (attr.Item2 == null || attr.Item2.Name != "Attach")
					continue;
				var attrDeclType = analyzer.ResolveType(attr.Item2.OwnerTypeId);
				if (attrDeclType.FullName != "Caliburn.Micro.Message")
					continue;

				string actionStr = prop.Value;
				foreach (var msg in actionStr.Split(';')) {
					string msgStr;
					if (msg.Contains("=")) {
						msgStr = msg.Split('=')[1].Trim('[', ']', ' ');
					}
					else {
						msgStr = msg.Trim('[', ']', ' ');
					}
					if (msgStr.StartsWith("Action"))
						msgStr = msgStr.Substring(6);
					int parenIndex = msgStr.IndexOf('(');
					if (parenIndex != -1)
						msgStr = msgStr.Substring(0, parenIndex);

					string actName = msgStr.Trim();
					foreach (var method in analyzer.LookupMethod(actName))
						analyzer.NameService.SetCanRename(method, false);
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
