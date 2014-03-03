using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Renamer.BAML;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class BAMLTypeReference : INameReference<TypeDef>
    {
        TypeSig sig;
        TypeInfoRecord rec;
        public BAMLTypeReference(TypeSig sig, TypeInfoRecord rec)
        {
            this.sig = sig;
            this.rec = rec;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            rec.TypeFullName = sig.ReflectionFullName;
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
