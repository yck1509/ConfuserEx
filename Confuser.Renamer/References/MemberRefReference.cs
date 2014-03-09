using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    public class MemberRefReference : INameReference<IDefinition>
    {
        MemberRef memberRef;
        IDefinition memberDef;
        public MemberRefReference(MemberRef memberRef, IDefinition memberDef)
        {
            this.memberRef = memberRef;
            this.memberDef = memberDef;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            memberRef.Name = memberDef.Name;
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
