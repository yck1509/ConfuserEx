using System;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A function that compare the type of definition.
	/// </summary>
	public class MemberTypeFunction : PatternFunction {
		internal const string FnName = "member-type";

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
			object type = Arguments[0].Evaluate(definition);

			if (definition is TypeDef)
				return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "type") == 0;

			if (definition is MethodDef)
				return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "method") == 0;

			if (definition is FieldDef)
				return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "field") == 0;

			if (definition is PropertyDef)
				return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "property") == 0;

			if (definition is EventDef)
				return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "event") == 0;

			if (definition is ModuleDef)
				return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "module") == 0;

			return false;
		}
	}
}