using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class CAMemberReference : INameReference<IDefinition>
    {
        CANamedArgument namedArg;
        IDefinition definition;
        public CAMemberReference(CANamedArgument namedArg, IDefinition definition)
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
