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
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.Protections.Constants
{
    class DynamicMode : IEncodeMode
    {
        class CodeGen : CILCodeGen
        {
            Local block;
            Local key;
            public CodeGen(Local block, Local key, MethodDef init, IList<Instruction> instrs)
                : base(init, instrs)
            {
                this.block = block;
                this.key = key;
            }
            protected override Local Var(Variable var)
            {
                if (var.Name == "{BUFFER}")
                    return block;
                else if (var.Name == "{KEY}")
                    return key;
                else
                    return base.Var(var);
            }
        }

        Action<uint[], uint[]> encryptFunc;

        public IEnumerable<Instruction> EmitDecrypt(MethodDef init, CEContext ctx, Local block, Local key)
        {
            StatementBlock encrypt, decrypt;
            ctx.DynCipher.GenerateCipherPair(ctx.Random, out encrypt, out decrypt);
            var ret = new List<Instruction>();

            var codeGen = new CodeGen(block, key, init, ret);
            codeGen.GenerateCIL(decrypt);
            codeGen.Commit(init.Body);

            var dmCodeGen = new DMCodeGen(typeof(void), new [] {
                Tuple.Create("{BUFFER}", typeof(uint[])),
                Tuple.Create("{KEY}", typeof(uint[]))
            });
            dmCodeGen.GenerateCIL(encrypt);
            encryptFunc = dmCodeGen.Compile<Action<uint[], uint[]>>();

            return ret;
        }

        public uint[] Encrypt(uint[] data, int offset, uint[] key)
        {
            uint[] ret = new uint[key.Length];
            Buffer.BlockCopy(data, offset * sizeof(uint), ret, 0, key.Length * sizeof(uint));
            encryptFunc(ret, key);
            return ret;
        }

        public object CreateDecoder(MethodDef decoder, CEContext ctx)
        {
            uint k1 = ctx.Random.NextUInt32() | 1;
            uint k2 = ctx.Random.NextUInt32();
            MutationHelper.ReplacePlaceholder(decoder, arg =>
            {
                List<Instruction> repl = new List<Instruction>();
                repl.AddRange(arg);
                repl.Add(Instruction.Create(OpCodes.Ldc_I4, (int)MathsUtils.modInv(k1)));
                repl.Add(Instruction.Create(OpCodes.Mul));
                repl.Add(Instruction.Create(OpCodes.Ldc_I4, (int)k2));
                repl.Add(Instruction.Create(OpCodes.Xor));
                return repl.ToArray();
            });
            return Tuple.Create(k1, k2);
        }

        public uint Encode(object data, CEContext ctx, uint id)
        {
            var key = (Tuple<uint, uint>)data;
            uint ret = (id ^ key.Item2) * key.Item1;
            Debug.Assert(((ret * MathsUtils.modInv(key.Item1)) ^ key.Item2) == id);
            return ret;
        }
    }
}
