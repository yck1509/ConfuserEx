using System;
using System.Text;
using System.Text.RegularExpressions;
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
			if (type == null && definition is IMemberDef)
				type = ((IMemberDef)definition).DeclaringType;
			if (type == null)
				return false;

			string typeRegex = Arguments[0].Evaluate(definition).ToString();

			var typeType = new StringBuilder();

			if (type.IsEnum)
				typeType.Append("enum ");

			if (type.IsInterface)
				typeType.Append("interface ");

			if (type.IsValueType)
				typeType.Append("valuetype ");

			if (type.IsDelegate())
				typeType.Append("delegate ");

			if (type.IsAbstract)
				typeType.Append("abstract ");

			if (type.IsNested)
				typeType.Append("nested ");

			if (type.IsSerializable)
				typeType.Append("serializable ");

			return Regex.IsMatch(typeType.ToString(), typeRegex);
		}
	}
}