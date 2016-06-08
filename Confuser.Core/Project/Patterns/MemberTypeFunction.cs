using System;
using System.Text;
using System.Text.RegularExpressions;
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
			string typeRegex = Arguments[0].Evaluate(definition).ToString();

			var memberType = new StringBuilder();

			if (definition is TypeDef)
				memberType.Append("type ");

			if (definition is MethodDef) {
				memberType.Append("method ");

				var method = (MethodDef)definition;
				if (method.IsGetter)
					memberType.Append("propertym getter ");
				else if (method.IsSetter)
					memberType.Append("propertym setter ");
				else if (method.IsAddOn)
					memberType.Append("eventm add ");
				else if (method.IsRemoveOn)
					memberType.Append("eventm remove ");
				else if (method.IsFire)
					memberType.Append("eventm fire ");
				else if (method.IsOther)
					memberType.Append("other ");
			}

			if (definition is FieldDef)
				memberType.Append("field ");

			if (definition is PropertyDef)
				memberType.Append("property ");

			if (definition is EventDef)
				memberType.Append("event ");

			if (definition is ModuleDef)
				memberType.Append("module ");

			return Regex.IsMatch(memberType.ToString(), typeRegex);
		}
	}
}