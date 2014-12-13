using System;
using System.Diagnostics;
using Confuser.Core;
using dnlib.DotNet.Emit;

namespace Confuser.Renamer.BAML {
	public class BAMLStringReference : IBAMLReference {
		Instruction instr;

		public BAMLStringReference(Instruction instr) {
			this.instr = instr;
		}

		public void Rename(string oldName, string newName) {
			var value = (string)instr.Operand;
			if (value.IndexOf(oldName, StringComparison.OrdinalIgnoreCase) != -1)
				value = value.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
			else if (oldName.EndsWith(".baml")) {
				Debug.Assert(newName.EndsWith(".baml"));
				var oldXaml = oldName.Substring(0, oldName.Length - 5) + ".xaml";
				var newXaml = newName.Substring(0, newName.Length - 5) + ".xaml";
				value = value.Replace(oldXaml, newXaml, StringComparison.OrdinalIgnoreCase);
			}
			else
				throw new UnreachableException();
			instr.Operand = value;
		}
	}
}