using System;
using Confuser.Core;
using Confuser.Renamer.BAML;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class BAMLEnumReference : INameReference<FieldDef> {

		private readonly FieldDef enumField;
		private readonly PropertyRecord rec;

		public BAMLEnumReference(FieldDef enumField, PropertyRecord rec) {
			this.enumField = enumField;
			this.rec = rec;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			rec.Value = enumField.Name;
			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}

	}
}