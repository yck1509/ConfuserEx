using System;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A function that indicate whether the item has the given custom attribute.
	/// </summary>
	public class HasAttrFunction : PatternFunction {
		internal const string FnName = "has-attr";

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
			string attrName = Arguments[0].Evaluate(definition).ToString();
			return definition.CustomAttributes.IsDefined(attrName);
		}
	}
}