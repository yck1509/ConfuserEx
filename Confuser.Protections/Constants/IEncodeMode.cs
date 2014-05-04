using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;

namespace Confuser.Protections.Constants
{
    interface IEncodeMode
    {
        IEnumerable<Instruction> EmitDecrypt(MethodDef init, CEContext ctx, Local block, Local key);
        uint[] Encrypt(uint[] data, int offset, uint[] key);

        object CreateDecoder(MethodDef decoder, CEContext ctx);
        uint Encode(object data, CEContext ctx, uint id);
    }
}
