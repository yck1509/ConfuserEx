using System;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     The AND operator.
	/// </summary>
	public class AndOperator : PatternOperator {
		internal const string OpName = "and";

		/// <inheritdoc />
		public override string Name {
			get { return OpName; }
		}

		/// <inheritdoc />
		public override bool IsUnary {
			get { return false; }
		}

		/// <inheritdoc />
		public override object Evaluate(IDnlibDef definition) {
			var a = (bool)OperandA.Evaluate(definition);
			if (!a) return false;
			return (bool)OperandB.Evaluate(definition);
		}
	}
}