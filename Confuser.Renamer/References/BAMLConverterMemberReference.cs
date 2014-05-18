using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Renamer.BAML;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class BAMLConverterMemberReference : INameReference<IDnlibDef>
    {
        BAMLAnalyzer.XmlNsContext xmlnsCtx;
        TypeSig sig;
        IDnlibDef member;
        PropertyRecord rec;
        public BAMLConverterMemberReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, IDnlibDef member, PropertyRecord rec)
        {
            this.xmlnsCtx = xmlnsCtx;
            this.sig = sig;
            this.member = member;
            this.rec = rec;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            string typeName = sig.ReflectionName;
            string prefix = xmlnsCtx.GetPrefix(sig.ReflectionNamespace, sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module.Assembly);
            if (!string.IsNullOrEmpty(prefix))
                typeName = prefix + ":" + typeName;
            rec.Value = typeName + "." + member.Name;
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
