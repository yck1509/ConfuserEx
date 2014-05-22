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
using Confuser.Core.Services;

namespace Confuser.Protections.Compress
{
    class DynamicDeriver : IKeyDeriver
    {
        class CodeGen : CILCodeGen
        {
            Local block;
            Local key;
            public CodeGen(Local block, Local key, MethodDef method, IList<Instruction> instrs)
                : base(method, instrs)
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

        StatementBlock derivation;
        Action<uint[], uint[]> encryptFunc;

        public void Init(ConfuserContext ctx, RandomGenerator random)
        {
            StatementBlock dummy;
            ctx.Registry.GetService<IDynCipherService>().GenerateCipherPair(random, out derivation, out dummy);

            var dmCodeGen = new DMCodeGen(typeof(void), new[] {
                Tuple.Create("{BUFFER}", typeof(uint[])),
                Tuple.Create("{KEY}", typeof(uint[]))
            });
            dmCodeGen.GenerateCIL(derivation);
            encryptFunc = dmCodeGen.Compile<Action<uint[], uint[]>>();
        }

        public uint[] DeriveKey(uint[] a, uint[] b)
        {
            uint[] ret = new uint[0x10];
            Buffer.BlockCopy(a, 0, ret, 0, a.Length * sizeof(uint));
            encryptFunc(ret, b);
            return ret;
        }

        public IEnumerable<Instruction> EmitDerivation(MethodDef method, ConfuserContext ctx, Local dst, Local src)
        {
            var ret = new List<Instruction>();
            var codeGen = new CodeGen(dst, src, method, ret);
            codeGen.GenerateCIL(derivation);
            codeGen.Commit(method.Body);
            return ret;
        }
    }
}
