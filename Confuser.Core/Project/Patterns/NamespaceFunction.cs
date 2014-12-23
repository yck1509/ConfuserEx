using System;
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
			object ns = Arguments[0].Evaluate(definition);

			var type = definition as TypeDef;
			if (type == null)
				type = ((IMemberDef)definition).DeclaringType;

			while (type.IsNested)
				type = type.DeclaringType;

			return type != null && type.Namespace == ns.ToString();
		}
	}
}