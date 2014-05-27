using Confuser.Core;
using Confuser.Renamer.BAML;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class BAMLTypeReference : INameReference<TypeDef> {
		private readonly TypeInfoRecord rec;
		private readonly TypeSig sig;

		public BAMLTypeReference(TypeSig sig, TypeInfoRecord rec) {
			this.sig = sig;
			this.rec = rec;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			rec.TypeFullName = sig.ReflectionFullName;
			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}
	}
}