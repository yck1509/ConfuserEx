using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using System.Diagnostics;
using Confuser.Renamer.References;

namespace Confuser.Renamer.Analyzers
{
    class GenericAnalyzer : IRenamer
    {
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
                AnalyzeMemberRef(context, service, memberRef);
            }


            // CustomAttribute
            table = module.TablesStream.Get(Table.CustomAttribute);
            len = table.Rows;
            var attrs = Enumerable.Range(1, (int)len)
                .Select(rid => module.ResolveHasCustomAttribute(module.TablesStream.ReadCustomAttributeRow((uint)rid).Parent))
                .Distinct()
                .SelectMany(owner => owner.CustomAttributes)
                .ToList();
            Debug.Assert(len == attrs.Count);
            foreach (var attr in attrs)
            {
                AnalyzeCustomAttributes(context, service, attr);
            }
        }

        void AnalyzeMemberRef(ConfuserContext context, INameService service, MemberRef memberRef)
        {
            var declType = memberRef.DeclaringType;
            TypeSpec typeSpec = declType as TypeSpec;
            if (typeSpec == null)
                return;

            TypeSig sig = typeSpec.TypeSig;
            while (sig.Next != null)
                sig = sig.Next;


            Debug.Assert(sig is TypeDefOrRefSig || sig is GenericInstSig);
            if (sig is GenericInstSig)
            {
                GenericInstSig inst = (GenericInstSig)sig;
                TypeDef openType = inst.GenericType.TypeDef;
                if (openType == null || openType.Module != memberRef.Module)
                    return;

                IDefinition member;
                if (memberRef.IsFieldRef) member = memberRef.ResolveFieldThrow();
                else if (memberRef.IsMethodRef) member = memberRef.ResolveMethodThrow();
                else throw new UnreachableException();

                service.AddReference(member, new GenericMemberRefReference(memberRef, member));
            }

        }

        void AnalyzeCustomAttributes(ConfuserContext context, INameService service, CustomAttribute attr)
        {
        }


        public void PreRename(ConfuserContext context, INameService service, IDefinition def)
        {
        }

        public void PostRename(ConfuserContext context, INameService service, IDefinition def)
        {
        }
    }
}
