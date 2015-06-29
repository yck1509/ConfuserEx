using System;
using System.Linq;
using System.Text.RegularExpressions;
using Confuser.Core;
using Confuser.Renamer.References;
using dnlib.DotNet;

namespace Confuser.Renamer.Analyzers {
	internal class ResourceAnalyzer : IRenamer {
		static readonly Regex ResourceNamePattern = new Regex("^(.*)\\.resources$");

		public void Analyze(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			var module = def as ModuleDef;
			if (module == null) return;

			string asmName = module.Assembly.Name.String;
			if (!string.IsNullOrEmpty(module.Assembly.Culture) &&
			    asmName.EndsWith(".resources")) {
				// Satellite assembly
				var satellitePattern = new Regex(string.Format("^(.*)\\.{0}\\.resources$", module.Assembly.Culture));
				string nameAsmName = asmName.Substring(0, asmName.Length - ".resources".Length);
				ModuleDef mainModule = context.Modules.SingleOrDefault(mod => mod.Assembly.Name == nameAsmName);
				if (mainModule == null) {
					context.Logger.ErrorFormat("Could not find main assembly of satellite assembly '{0}'.", module.Assembly.FullName);
					throw new ConfuserException(null);
				}

				string format = "{0}." + module.Assembly.Culture + ".resources";
				foreach (Resource res in module.Resources) {
					Match match = satellitePattern.Match(res.Name);
					if (!match.Success)
						continue;
					string typeName = match.Groups[1].Value;
					TypeDef type = mainModule.FindReflectionThrow(typeName);
					if (type == null) {
						context.Logger.WarnFormat("Could not find resource type '{0}'.", typeName);
						continue;
					}
					service.ReduceRenameMode(type, RenameMode.ASCII);
					service.AddReference(type, new ResourceReference(res, type, format));
				}
			}
			else {
				string format = "{0}.resources";
				foreach (Resource res in module.Resources) {
					Match match = ResourceNamePattern.Match(res.Name);
					if (!match.Success || res.ResourceType != ResourceType.Embedded)
						continue;
					string typeName = match.Groups[1].Value;

					if (typeName.EndsWith(".g")) // WPF resources, ignore
						continue;

					TypeDef type = module.FindReflection(typeName);
					if (type == null) {
						context.Logger.WarnFormat("Could not find resource type '{0}'.", typeName);
						continue;
					}
					service.ReduceRenameMode(type, RenameMode.ASCII);
					service.AddReference(type, new ResourceReference(res, type, format));
				}
			}
		}

		public void PreRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}

		public void PostRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}
	}
}