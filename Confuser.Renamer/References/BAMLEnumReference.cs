using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Renamer.BAML;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class BAMLEnumReference : INameReference<FieldDef>
    {
        FieldDef enumField;
        PropertyRecord rec;
        public BAMLEnumReference(FieldDef enumField, PropertyRecord rec)
        {
            this.enumField = enumField;
            this.rec = rec;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            rec.Value = enumField.Name;
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
