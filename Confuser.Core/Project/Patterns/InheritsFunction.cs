using System;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A function that indicate whether the type inherits from the specified type.
	/// </summary>
	public class InheritsFunction : PatternFunction {

		internal const string FnName = "inherits";

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
			string name = Arguments[0].Evaluate(definition).ToString();
			if (!(definition is TypeDef))
				return false;
			var type = (TypeDef)definition;
			while (type.BaseType != null) {
				type = type.BaseType.ResolveTypeDefThrow();
				if (type.FullName == name)
					return true;
			}
			return false;
		}

	}
}