using System;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A function that compare the full name of declaring type.
	/// </summary>
	public class DeclTypeFunction : PatternFunction {
		internal const string FnName = "decl-type";

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
			if (!(definition is IMemberDef) || ((IMemberDef)definition).DeclaringType == null)
				return false;
			object fullName = Arguments[0].Evaluate(definition);
			return ((IMemberDef)definition).DeclaringType.FullName == fullName.ToString();
		}
	}
}