using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using System.Text.RegularExpressions;
using Confuser.Renamer.References;

namespace Confuser.Renamer.Analyzers
{
    class ResourceAnalyzer : IRenamer
    {
        static readonly Regex ResourceNamePattern = new Regex("^(.*)\\.resources$");

        public void Analyze(ConfuserContext context, INameService service, IDnlibDef def)
        {
            ModuleDef module = def as ModuleDef;
            if (module == null) return;

            string asmName = module.Assembly.Name.String;
            if (!string.IsNullOrEmpty(module.Assembly.Culture) &&
                asmName.EndsWith(".resources"))
            {
                // Satellite assembly
                Regex satellitePattern = new Regex(string.Format("^(.*)\\.{0}\\.resources$", module.Assembly.Culture));
                string nameAsmName = asmName.Substring(0,asmName.Length - ".resources".Length);
                ModuleDef mainModule = context.Modules.SingleOrDefault(mod => mod.Assembly.Name == nameAsmName);
                if (mainModule == null)
                {
                    context.Logger.ErrorFormat("Could not found main assembly of satellite assembly '{0}'.", module.Assembly.FullName);
                    throw new ConfuserException(null);
                }

                string format = "{0}.{1}." + module.Assembly.Culture + ".resources";
                foreach (var res in module.Resources)
                {
                    Match match = satellitePattern.Match(res.Name);
                    if (!match.Success)
                        continue;
                    string typeName = match.Groups[1].Value;
                    TypeDef type = mainModule.FindReflectionThrow(typeName);
                    if (type == null)
                    {
                        context.Logger.WarnFormat("Could not found resource type '{0}'.", typeName);
                        continue;
                    }
                    service.AddReference(type, new ResourceReference(res, type, format));
                }
            }
            else
            {
                string format = "{0}.{1}.resources";
                foreach (var res in module.Resources)
                {
                    Match match = ResourceNamePattern.Match(res.Name);
                    if (!match.Success)
                        continue;
                    string typeName = match.Groups[1].Value;

                    if (typeName.EndsWith(".g")) // WPF resources, ignore
                        continue;

                    TypeDef type = module.FindReflection(typeName);
                    if (type == null)
                    {
                        context.Logger.WarnFormat("Could not found resource type '{0}'.", typeName);
                        continue;
                    }
                    service.AddReference(type, new ResourceReference(res, type, format));
                }
            }
        }

        public void PreRename(ConfuserContext context, INameService service, IDnlibDef def)
        {
            //
        }

        public void PostRename(ConfuserContext context, INameService service, IDnlibDef def)
        {
            //
        }
    }
}
