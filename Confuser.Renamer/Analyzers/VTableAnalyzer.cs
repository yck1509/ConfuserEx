using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Renamer.References;

namespace Confuser.Renamer.Analyzers
{
    class VTableAnalyzer : IRenamer
    {
        public void Analyze(ConfuserContext context, INameService service, IDefinition def)
        {
            MethodDef method = def as MethodDef;
            if (method == null || !method.IsVirtual)
                return;

            var vTbl = service.GetVTables()[method.DeclaringType];
            var sig = VTableSignature.FromMethod(method);
            var slot = vTbl.FindSlot(method);
            Debug.Assert(slot != null);

            if (method.IsAbstract)
            {
                service.SetCanRename(method, false);
            }
            else
            {
                foreach (var baseSlot in slot.Overrides)
                {
                    // Better on safe side, add references to both methods.
                    service.AddReference(method, new OverrideDirectiveReference(slot, baseSlot));
                    service.AddReference(baseSlot.MethodDef, new OverrideDirectiveReference(slot, baseSlot));
                }
            }
        }


        public void PreRename(ConfuserContext context, INameService service, IDefinition def)
        {
            //
        }

        class MethodDefOrRefComparer : IEqualityComparer<IMethodDefOrRef>
        {
            private MethodDefOrRefComparer() { }

            public static readonly MethodDefOrRefComparer Instance = new MethodDefOrRefComparer();

            public bool Equals(IMethodDefOrRef x, IMethodDefOrRef y)
            {
                return new SigComparer().Equals(x, y) && new SigComparer().Equals(x.DeclaringType, y.DeclaringType);
            }

            public int GetHashCode(IMethodDefOrRef obj)
            {
                return new SigComparer().GetHashCode(obj) * 5 + new SigComparer().GetHashCode(obj.DeclaringType);
            }
        }

        public void PostRename(ConfuserContext context, INameService service, IDefinition def)
        {
            MethodDef method = def as MethodDef;
            if (method == null || !method.IsVirtual || method.Overrides.Count == 0)
                return;

            HashSet<IMethodDefOrRef> methods = new HashSet<IMethodDefOrRef>(MethodDefOrRefComparer.Instance);
            method.Overrides
                .RemoveWhere(impl => MethodDefOrRefComparer.Instance.Equals(impl.MethodDeclaration, method));
        }
    }
}
