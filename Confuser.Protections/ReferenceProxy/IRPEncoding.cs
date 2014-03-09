using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;

namespace Confuser.Protections.ReferenceProxy
{
    interface IRPEncoding
    {
        Instruction[] EmitDecode(MethodDef init, RPContext ctx, Instruction[] arg);
        int Encode(MethodDef init, RPContext ctx, int value);
    }
}
