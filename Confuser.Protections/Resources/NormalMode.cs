using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;
using Confuser.Core.Helpers;
using Confuser.Core;
using Confuser.DynCipher;
using System.Diagnostics;

namespace Confuser.Protections.Resources
{
    class NormalMode : IEncodeMode
    {
        public IEnumerable<Instruction> EmitDecrypt(MethodDef init, REContext ctx, Local block, Local key)
        {
            for (int i = 0; i < 0x10; i++)
            {
                yield return Instruction.Create(OpCodes.Ldloc, block);
                yield return Instruction.Create(OpCodes.Ldc_I4, i);
                yield return Instruction.Create(OpCodes.Ldloc, block);
                yield return Instruction.Create(OpCodes.Ldc_I4, i);
                yield return Instruction.Create(OpCodes.Ldelem_U4);
                yield return Instruction.Create(OpCodes.Ldloc, key);
                yield return Instruction.Create(OpCodes.Ldc_I4, i);
                yield return Instruction.Create(OpCodes.Ldelem_U4);
                yield return Instruction.Create(OpCodes.Xor);
                yield return Instruction.Create(OpCodes.Stelem_I4);
            }
        }

        public uint[] Encrypt(uint[] data, int offset, uint[] key)
        {
            uint[] ret = new uint[key.Length];
            for (int i = 0; i < key.Length; i++)
                ret[i] = data[i + offset] ^ key[i];
            return ret;
        }
    }
}
