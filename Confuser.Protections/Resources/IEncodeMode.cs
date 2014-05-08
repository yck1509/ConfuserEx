using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;

namespace Confuser.Protections.Resources
{
    interface IEncodeMode
    {
        IEnumerable<Instruction> EmitDecrypt(MethodDef init, REContext ctx, Local block, Local key);
        uint[] Encrypt(uint[] data, int offset, uint[] key);
    }
}
