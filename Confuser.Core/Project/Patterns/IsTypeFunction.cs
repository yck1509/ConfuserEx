using System;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A function that indicate the type of type(?).
	/// </summary>
	public class IsTypeFunction : PatternFunction {

		internal const string FnName = "is-type";

		/// <inheritdoc />
		public override string Name {
			get { return FnName; }
		}

		/// <inheritdoc />
		public override int ArgumentCount {
			get { return 1; }
		}

		/// <inheritdoc />
		public override object Evaluate(IDnlibDef definition) {
			TypeDef type = definition as TypeDef;
			if (type == null)
				return false;

			string typeType = Arguments[0].Evaluate(definition).ToString();

			if (type.IsEnum)
				return StringComparer.OrdinalIgnoreCase.Compare(typeType, "enum") == 0;

			if (type.IsInterface)
				return StringComparer.OrdinalIgnoreCase.Compare(typeType, "interface") == 0;

			if (type.IsValueType)
				return StringComparer.OrdinalIgnoreCase.Compare(typeType, "valuetype") == 0;

			if (type.IsDelegate())
				return StringComparer.OrdinalIgnoreCase.Compare(typeType, "delegate") == 0;

			if (type.IsAbstract)
				return StringComparer.OrdinalIgnoreCase.Compare(typeType, "abstract") == 0;

			return false;
		}

	}
}