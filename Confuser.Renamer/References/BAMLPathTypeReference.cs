using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Renamer.BAML;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class BAMLPathTypeReference : INameReference<TypeDef>
    {
        BAMLAnalyzer.XmlNsContext xmlnsCtx;
        TypeSig sig;
        PropertyPathIndexer indexer;
        PropertyPathPart attachedDP;

        public BAMLPathTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, PropertyPathIndexer indexer)
        {
            this.xmlnsCtx = xmlnsCtx;
            this.sig = sig;
            this.indexer = indexer;
            this.attachedDP = null;
        }

        public BAMLPathTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, PropertyPathPart attachedDP)
        {
            this.xmlnsCtx = xmlnsCtx;
            this.sig = sig;
            this.indexer = null;
            this.attachedDP = attachedDP;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            string name = sig.ReflectionName;
            string prefix = xmlnsCtx.GetPrefix(sig.ReflectionNamespace, sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module.Assembly);
            if (!string.IsNullOrEmpty(prefix))
                name = prefix + ":" + name;
            if (indexer != null)
            {
                indexer.Type = name;
            }
            else
            {
                string oldType, property;
                attachedDP.ExtractAttachedDP(out oldType, out property);
                attachedDP.Name = string.Format("({0}.{1})", name, property);
            }
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}
