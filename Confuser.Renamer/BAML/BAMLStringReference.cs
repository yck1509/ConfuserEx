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

		public bool CanRename(string oldName, string newName) {
			// TODO: Other protection interfering
			return instr.OpCode.Code == Code.Ldstr;
		}

		public void Rename(string oldName, string newName) {
			var value = (string)instr.Operand;
			if (value.IndexOf(oldName, StringComparison.OrdinalIgnoreCase) != -1)
				value = newName;
			else if (oldName.EndsWith(".baml")) {
				Debug.Assert(newName.EndsWith(".baml"));
				/*
                 * Nik's patch for maintaining relative paths. If the xaml file is referenced in this manner
                 * "/some.namespace;component/somefolder/somecontrol.xaml"
                 * then we want to keep the relative path and namespace intact. We should be obfuscating it like this - /some.namespace;component/somefolder/asjdjh2398498dswk.xaml
                 * */
				//value = newName.Substring(0, newName.Length - 5) + ".xaml";
				value = value.Replace(oldName.Replace(".baml", string.Empty, StringComparison.InvariantCultureIgnoreCase),
				                      newName.Replace(".baml", String.Empty, StringComparison.InvariantCultureIgnoreCase),
				                      StringComparison.InvariantCultureIgnoreCase);
			}
			else
				throw new UnreachableException();
			instr.Operand = value;
		}
	}
}