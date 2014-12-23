using System;
using Confuser.Core;
using Confuser.Renamer.BAML;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class BAMLPathTypeReference : INameReference<TypeDef> {
		readonly PropertyPathPart attachedDP;
		readonly PropertyPathIndexer indexer;
		readonly TypeSig sig;
		readonly BAMLAnalyzer.XmlNsContext xmlnsCtx;

		public BAMLPathTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, PropertyPathIndexer indexer) {
			this.xmlnsCtx = xmlnsCtx;
			this.sig = sig;
			this.indexer = indexer;
			attachedDP = null;
		}

		public BAMLPathTypeReference(BAMLAnalyzer.XmlNsContext xmlnsCtx, TypeSig sig, PropertyPathPart attachedDP) {
			this.xmlnsCtx = xmlnsCtx;
			this.sig = sig;
			indexer = null;
			this.attachedDP = attachedDP;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			string name = sig.ReflectionName;
			string prefix = xmlnsCtx.GetPrefix(sig.ReflectionNamespace, sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module.Assembly);
			if (!string.IsNullOrEmpty(prefix))
				name = prefix + ":" + name;
			if (indexer != null) {
				indexer.Type = name;
			}
			else {
				string oldType, property;
				attachedDP.ExtractAttachedDP(out oldType, out property);
				attachedDP.Name = string.Format("({0}.{1})", name, property);
			}
			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}
	}
}