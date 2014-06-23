using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal interface IEncodeMode {
		IEnumerable<Instruction> EmitDecrypt(MethodDef init, CEContext ctx, Local block, Local key);
		uint[] Encrypt(uint[] data, int offset, uint[] key);

		object CreateDecoder(MethodDef decoder, CEContext ctx);
		uint Encode(object data, CEContext ctx, uint id);
	}
}