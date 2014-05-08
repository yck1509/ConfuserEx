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

namespace Confuser.Protections.Resources
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

        public IEnumerable<Instruction> EmitDecrypt(MethodDef init, REContext ctx, Local block, Local key)
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
    }
}
