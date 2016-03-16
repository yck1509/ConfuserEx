using System;
using System.Text.RegularExpressions;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A function that compare the namespace of definition.
	/// </summary>
	public class NamespaceFunction : PatternFunction {
		internal const string FnName = "namespace";

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
			if (!(definition is TypeDef) && !(definition is IMemberDef))
				return false;
			var ns = "^" + Arguments[0].Evaluate(definition).ToString() + "$";

			var type = definition as TypeDef;
			if (type == null)
				type = ((IMemberDef)definition).DeclaringType;

			if (type == null)
				return false;

			while (type.IsNested)
				type = type.DeclaringType;

			return type != null && Regex.IsMatch(type.Namespace ?? "", ns);
		}
	}
}