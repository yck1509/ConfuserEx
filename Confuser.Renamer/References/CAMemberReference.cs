using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class CAMemberReference : INameReference<IDnlibDef>
    {
        CANamedArgument namedArg;
        IDnlibDef definition;
        public CAMemberReference(CANamedArgument namedArg, IDnlibDef definition)
        {
            this.namedArg = namedArg;
            this.definition = definition;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            namedArg.Name = definition.Name;
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
