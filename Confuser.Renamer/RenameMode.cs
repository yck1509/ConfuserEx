using System;

namespace Confuser.Renamer {
	public enum RenameMode {
		Empty = 0x0,
		Unicode = 0x1,
		ASCII = 0x2,
		Letters = 0x3,

		Decodable = 0x10,
		Sequential = 0x11,
		Reversible = 0x12,

		Debug = 0x20
	}
}