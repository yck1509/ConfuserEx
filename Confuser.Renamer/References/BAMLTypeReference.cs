using System;
using Confuser.Core;
using Confuser.Renamer.BAML;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class BAMLTypeReference : INameReference<TypeDef> {
		readonly TypeInfoRecord rec;
		readonly TypeSig sig;

		public BAMLTypeReference(TypeSig sig, TypeInfoRecord rec) {
			this.sig = sig;
			this.rec = rec;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			rec.TypeFullName = sig.ReflectionFullName;
			return true;
		}

		public bool ShouldCancelRename() {
			// For GenericInstSig we will have sig.ReflectionFullName refer to the old
			// (unobfuscated) name, even if it should be obfuscated. Thus #424 created. If fixed,
			// this line could be replaced with return false; as it was before.
			return sig is GenericInstSig;
		}
	}
}