using System;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class ResourceReference : INameReference<TypeDef> {
		private readonly string format;
		private readonly Resource resource;
		private readonly TypeDef typeDef;

		public ResourceReference(Resource resource, TypeDef typeDef, string format) {
			this.resource = resource;
			this.typeDef = typeDef;
			this.format = format;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			resource.Name = string.Format(format, typeDef.ReflectionFullName);
			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}
	}
}