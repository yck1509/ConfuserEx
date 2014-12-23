using System;
using System.Windows.Markup;

namespace ConfuserEx {
	public class EnumValuesExtension : MarkupExtension {
		readonly Type enumType;

		public EnumValuesExtension(Type enumType) {
			this.enumType = enumType;
		}

		public override object ProvideValue(IServiceProvider serviceProvider) {
			return Enum.GetValues(enumType);
		}
	}
}