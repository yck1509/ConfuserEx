using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;
using System.Diagnostics;

namespace Confuser.Renamer.References
{
    class OverrideDirectiveReference : INameReference<MethodDef>
    {
        VTableSlot thisSlot;
        VTableSlot baseSlot;
        public OverrideDirectiveReference(VTableSlot thisSlot, VTableSlot baseSlot)
        {
            this.thisSlot = thisSlot;
            this.baseSlot = baseSlot;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            var method = thisSlot.MethodDef;

            IMethodDefOrRef target;
            if (baseSlot.DeclaringType is GenericInstSig)
            {
                var declType = (GenericInstSig)baseSlot.DeclaringType;
                target = new MemberRefUser(method.Module, baseSlot.MethodDef.Name, baseSlot.MethodDef.MethodSig, declType.ToTypeDefOrRef());
                target = (IMethodDefOrRef)method.Module.Import(target);
            }
            else
            {
                target = baseSlot.MethodDef;
                if (target.Module != method.Module)
                    target = method.Module.Import(baseSlot.MethodDef);
            }

            if (method.Overrides.Any(impl =>
                new SigComparer().Equals(impl.MethodDeclaration.MethodSig, target.MethodSig) &&
                new SigComparer().Equals(impl.MethodDeclaration.DeclaringType.ResolveTypeDef(), target.DeclaringType.ResolveTypeDef())))
                return true;

            method.Overrides.Add(new MethodOverride(method, target));

            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
