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
				value = newName;
			else if (oldName.EndsWith(".baml")) {
				Debug.Assert(newName.EndsWith(".baml"));
				value = newName.Substring(0, newName.Length - 5) + ".xaml";
			}
			else
				throw new UnreachableException();
			instr.Operand = value;
		}
	}
}