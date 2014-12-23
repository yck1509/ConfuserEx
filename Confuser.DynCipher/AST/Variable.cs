using System;

namespace Confuser.DynCipher.AST {
	public class Variable {
		public Variable(string name) {
			Name = name;
		}

		public string Name { get; set; }
		public object Tag { get; set; }

		public override string ToString() {
			return Name;
		}
	}
}