using System;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A function that compare the module of definition.
	/// </summary>
	public class ModuleFunction : PatternFunction {
		internal const string FnName = "module";

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
			if (!(definition is IOwnerModule) && !(definition is IModule))
				return false;
			object name = Arguments[0].Evaluate(definition);
			if (definition is IModule)
				return ((IModule)definition).Name == name.ToString();
			return ((IOwnerModule)definition).Module.Name == name.ToString();
		}
	}
}