using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Renamer.BAML;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class BAMLAttributeReference : INameReference<IDefinition>
    {
        IDefinition member;
        AttributeInfoRecord rec;
        public BAMLAttributeReference(IDefinition member, AttributeInfoRecord rec)
        {
            this.member = member;
            this.rec = rec;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            rec.Name = member.Name;
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
