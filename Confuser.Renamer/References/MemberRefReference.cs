using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    public class MemberRefReference : INameReference<IDnlibDef>
    {
        MemberRef memberRef;
        IDnlibDef memberDef;
        public MemberRefReference(MemberRef memberRef, IDnlibDef memberDef)
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
