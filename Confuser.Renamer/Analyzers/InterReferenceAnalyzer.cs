using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Core.Services;
using dnlib.DotNet.MD;
using Confuser.Renamer.References;

namespace Confuser.Renamer.Analyzers
{
    class InterReferenceAnalyzer : IRenamer
    {
        // i.e. Inter-Assembly References, e.g. InternalVisibleToAttributes

        public void Analyze(ConfuserContext context, INameService service, IDnlibDef def)
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
                if (declType.Module != module && context.Modules.Contains((ModuleDefMD)declType.Module))
                {
                    IDnlibDef memberDef = (IDnlibDef)declType.ResolveThrow(memberRef);
                    service.AddReference(memberDef, new MemberRefReference(memberRef, memberDef));
                }
            }

            // TypeRef
            table = module.TablesStream.Get(Table.TypeRef);
            len = table.Rows;
            for (uint i = 1; i <= len; i++)
            {
                TypeRef typeRef = module.ResolveTypeRef(i);

                TypeDef typeDef = typeRef.ResolveTypeDefThrow();
                if (typeDef.Module != module && context.Modules.Contains((ModuleDefMD)typeDef.Module))
                {
                    service.AddReference(typeDef, new TypeRefReference(typeRef, typeDef));
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
