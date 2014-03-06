using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow
{
    interface IPredicate
    {
        void Init(CilBody body);
        void EmitSwitchLoad(IList<Instruction> instrs);
        int GetSwitchKey(int key);
    }
}
