using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;

namespace Confuser.Protections.ReferenceProxy
{
    class NormalEncoding : IRPEncoding
    {
        Dictionary<MethodDef, Tuple<int, int>> keys = new Dictionary<MethodDef, Tuple<int, int>>();

        Tuple<int, int> GetKey(RandomGenerator random, MethodDef init)
        {
            Tuple<int, int> ret;
            if (!keys.TryGetValue(init, out ret))
            {
                int key = random.NextInt32() | 1;
                keys[init] = ret = Tuple.Create(key, (int)MathsUtils.modInv((uint)key));
            }
            return ret;
        }

        public Instruction[] EmitDecode(MethodDef init, RPContext ctx, Instruction[] arg)
        {
            var key = GetKey(ctx.Random, init);
            List<Instruction> ret = new List<Instruction>();
            if (ctx.Random.NextBoolean())
            {
                ret.Add(Instruction.Create(OpCodes.Ldc_I4, key.Item1));
                ret.AddRange(arg);
            }
            else
            {
                ret.AddRange(arg);
                ret.Add(Instruction.Create(OpCodes.Ldc_I4, key.Item1));
            }
            ret.Add(Instruction.Create(OpCodes.Mul));
            return ret.ToArray();
        }

        public int Encode(MethodDef init, RPContext ctx, int value)
        {
            var key = GetKey(ctx.Random, init);
            return value * key.Item2;
        }
    }
}
