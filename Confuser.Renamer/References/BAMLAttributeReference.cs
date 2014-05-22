using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Renamer.BAML;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class BAMLAttributeReference : INameReference<IDnlibDef>
    {
        IDnlibDef member;
        AttributeInfoRecord attrRec;
        PropertyRecord propRec;
        public BAMLAttributeReference(IDnlibDef member, AttributeInfoRecord rec)
        {
            this.member = member;
            this.attrRec = rec;
        }
        public BAMLAttributeReference(IDnlibDef member, PropertyRecord rec)
        {
            this.member = member;
            this.propRec = rec;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            if (attrRec != null)
                attrRec.Name = member.Name;
            else
                propRec.Value = member.Name;
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
