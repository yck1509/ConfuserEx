using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;
using dnlib.DotNet.Emit;
using Confuser.Core.Services;

namespace Confuser.Protections.Compress
{
    enum Mode
    {
        Normal,
        Dynamic
    }
    interface IKeyDeriver
    {
        void Init(ConfuserContext ctx, RandomGenerator random);
        uint[] DeriveKey(uint[] a, uint[] b);
        IEnumerable<Instruction> EmitDerivation(MethodDef method, ConfuserContext ctx, Local dst, Local src);
    }
}
