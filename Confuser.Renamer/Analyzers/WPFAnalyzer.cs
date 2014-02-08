using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Core.Services;

namespace Confuser.Renamer.Analyzers
{
    class WPFAnalyzer : IRenamer
    {
        public void Analyze(ConfuserContext context, INameService service, IDefinition def)
        {
            MethodDef method = def as MethodDef;
            if (method == null || !method.HasBody)
                return;

            ITraceService traceSrv = context.Registry.GetService<ITraceService>();
            var trace = traceSrv.Trace(method);
        }

        public void PreRename(ConfuserContext context, INameService service, IDefinition def)
        {
            //
        }

        public void PostRename(ConfuserContext context, INameService service, IDefinition def)
        {
            //
        }
    }
}
