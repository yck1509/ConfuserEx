using System;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     The NOT operator.
	/// </summary>
	public class NotOperator : PatternOperator {
		internal const string OpName = "not";

		/// <inheritdoc />
		public override string Name {
			get { return OpName; }
		}

		/// <inheritdoc />
		public override bool IsUnary {
			get { return true; }
		}

		/// <inheritdoc />
		public override object Evaluate(IDnlibDef definition) {
			return !(bool)OperandA.Evaluate(definition);
		}
	}
}