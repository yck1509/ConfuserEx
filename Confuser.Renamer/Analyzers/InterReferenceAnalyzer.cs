using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Core.Services;
using dnlib.DotNet.MD;

namespace Confuser.Renamer.Analyzers
{
    class InterReferenceAnalyzer: IRenamer
    {
        // i.e. Inter-Assembly References, e.g. InternalVisibleToAttributes
        
        public void Analyze(ConfuserContext context, INameService service, IDefinition def)
        {
            ModuleDefMD module = def as ModuleDefMD;
            if (module == null) return;

            MDTable table;
            uint len;

            // MemberRef
            table = module.TablesStream.Get(Table.MemberRef);
            len = table.Rows;
            for (uint i = 1; i <= len; i++)
            {
                MemberRef memberRef = module.ResolveMemberRef(i);

                TypeDef declType = memberRef.DeclaringType.ResolveTypeDefThrow();
                if (context.Modules.Contains((ModuleDefMD)declType.Module))
                {
                    
                }
            }

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
