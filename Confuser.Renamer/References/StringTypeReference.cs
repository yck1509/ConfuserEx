using System;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Renamer.References {
	public class StringTypeReference : INameReference<TypeDef> {
		readonly Instruction reference;
		readonly TypeDef typeDef;

		public StringTypeReference(Instruction reference, TypeDef typeDef) {
			this.reference = reference;
			this.typeDef = typeDef;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			reference.Operand = typeDef.ReflectionFullName;
			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}
	}
}