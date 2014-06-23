using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy {
	internal interface IRPEncoding {
		Instruction[] EmitDecode(MethodDef init, RPContext ctx, Instruction[] arg);
		int Encode(MethodDef init, RPContext ctx, int value);
	}
}