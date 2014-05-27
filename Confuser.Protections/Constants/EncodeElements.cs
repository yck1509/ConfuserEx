using System;

namespace Confuser.Protections.Constants {
	[Flags]
	internal enum EncodeElements {
		Strings = 1,
		Numbers = 2,
		Primitive = 4,
		Initializers = 8
	}
}