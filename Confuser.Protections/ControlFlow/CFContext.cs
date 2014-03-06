using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Services;
using dnlib.DotNet.Emit;
using dnlib.DotNet;
using Confuser.Core;
using Confuser.Renamer;
using Confuser.DynCipher;

namespace Confuser.Protections.ControlFlow
{
    enum CFType
    {
        Switch,
        Jump,
    }

    enum PredicateType
    {
        Normal,
        Expression,
        x86,
    }

    class CFContext
    {
        public RandomGenerator Random;
        public ConfuserContext Context;
        public MethodDef Method;
        public IDynCipherService DynCipher;

        public CFType Type;
        public PredicateType Predicate;
        public double Intensity;
        public int Depth;
        public bool JunkCode;
        public bool FakeBranch;

        public void AddJump(IList<Instruction> instrs, Instruction target)
        {
            if (!Method.Module.IsClr40 && JunkCode &&
                !Method.DeclaringType.HasGenericParameters && !Method.HasGenericParameters &&
                (instrs[0].OpCode.FlowControl == FlowControl.Call || instrs[0].OpCode.FlowControl == FlowControl.Next))
            {
                switch (Random.NextInt32(3))
                {
                    case 0:
                        instrs.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        instrs.Add(Instruction.Create(OpCodes.Brtrue, instrs[0]));
                        break;

                    case 1:
                        instrs.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                        instrs.Add(Instruction.Create(OpCodes.Brfalse, instrs[0]));
                        break;

                    case 2:     // Take that, de4dot + ILSpy :)
                        instrs.Add(Instruction.Create(OpCodes.Ldc_I4, Random.NextBoolean() ? 0 : 1));
                        instrs.Add(Instruction.Create(OpCodes.Box, Method.Module.CorLibTypes.Int32.TypeDefOrRef));
                        instrs.Add(Instruction.Create(OpCodes.Brfalse, instrs[0]));
                        break;
                }
            }

            instrs.Add(Instruction.Create(OpCodes.Br, target));
        }

        public void AddJunk(IList<Instruction> instrs)
        {
            if (Method.Module.IsClr40 || !JunkCode)
                return;

            switch (Random.NextInt32(6))
            {
                case 0:
                    instrs.Add(Instruction.Create(OpCodes.Pop));
                    break;
                case 1:
                    instrs.Add(Instruction.Create(OpCodes.Dup));
                    break;
                case 2:
                    instrs.Add(Instruction.Create(OpCodes.Throw));
                    break;
                case 3:
                    instrs.Add(Instruction.Create(OpCodes.Ldarg, new Parameter(0xff)));
                    break;
                case 4:
                    instrs.Add(Instruction.Create(OpCodes.Ldloc, new Local(null) { Index = 0xff }));
                    break;
                case 5:
                    break;
            }
        }
    }
}
