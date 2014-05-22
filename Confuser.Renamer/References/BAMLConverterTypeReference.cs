using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Renamer.BAML;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class BAMLConverterTypeReference : INameReference<TypeDef>
    {
        BAMLAnalyzer.XmlNsContext xmlnsCtx;
        TypeSig sig;
        PropertyRecord propRec;
        TextRecord textRec;
        public BAMLConverterTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, PropertyRecord rec)
        {
            this.xmlnsCtx = xmlnsCtx;
            this.sig = sig;
            this.propRec = rec;
        }
        public BAMLConverterTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, TextRecord rec)
        {
            this.xmlnsCtx = xmlnsCtx;
            this.sig = sig;
            this.textRec = rec;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            string name = sig.ReflectionName;
            string prefix = xmlnsCtx.GetPrefix(sig.ReflectionNamespace, sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module.Assembly);
            if (!string.IsNullOrEmpty(prefix))
                name = prefix + ":" + name;
            if (propRec != null)
                propRec.Value = name;
            else
                textRec.Value = name;
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
