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
using dnlib.DotNet.Writer;

namespace Confuser.Protections.Constants
{
    class x86Mode : IEncodeMode
    {
        class CipherCodeGen : CILCodeGen
        {
            Local block;
            Local key;
            public CipherCodeGen(Local block, Local key, MethodDef init, IList<Instruction> instrs)
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

        class x86Encoding
        {
            Expression expression;
            Expression inverse;
            byte[] code;
            dnlib.DotNet.Writer.MethodBody codeChunk;

            public Func<int, int> expCompiled;
            public MethodDef native;

            public void Compile(CEContext ctx)
            {
                Variable var = new Variable("{VAR}");
                Variable result = new Variable("{RESULT}");

                var int32 = ctx.Module.CorLibTypes.Int32;
                native = new MethodDefUser("", MethodSig.CreateStatic(int32, int32), MethodAttributes.PinvokeImpl | MethodAttributes.PrivateScope | MethodAttributes.Static);
                native.ImplAttributes = MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
                // Attempt to improve performance --- failed with StackOverflowException... :/
                //var suppressAttr = ctx.Method.Module.CorLibTypes.GetTypeRef("System.Security", "SuppressUnmanagedCodeSecurityAttribute").ResolveThrow();
                //native.CustomAttributes.Add(new CustomAttribute((MemberRef)ctx.Method.Module.Import(suppressAttr.FindDefaultConstructor())));
                //native.HasSecurity = true;
                ctx.Module.GlobalType.Methods.Add(native);

                ctx.Name.MarkHelper(native, ctx.Marker);

                x86Register? reg;
                var codeGen = new x86CodeGen();
                do
                {
                    ctx.DynCipher.GenerateExpressionPair(
                        ctx.Random,
                        new VariableExpression() { Variable = var }, new VariableExpression() { Variable = result },
                        4, out expression, out inverse);

                    reg = codeGen.GenerateX86(inverse, (v, r) =>
                    {
                        return new[] { x86Instruction.Create(x86OpCode.POP, new x86RegisterOperand(r)) };
                    });
                } while (reg == null);

                code = CodeGenUtils.AssembleCode(codeGen, reg.Value);

                expCompiled = new DMCodeGen(typeof(int), new[] { Tuple.Create("{VAR}", typeof(int)) })
                    .GenerateCIL(expression)
                    .Compile<Func<int, int>>();


                ctx.Context.CurrentModuleWriterListener.OnWriterEvent += InjectNativeCode;
            }

            void InjectNativeCode(object sender, ModuleWriterListenerEventArgs e)
            {
                ModuleWriter writer = (ModuleWriter)sender;
                if (e.WriterEvent == ModuleWriterEvent.MDEndWriteMethodBodies)
                {
                    codeChunk = writer.MethodBodies.Add(new dnlib.DotNet.Writer.MethodBody(code));
                }
                else if (e.WriterEvent == ModuleWriterEvent.EndCalculateRvasAndFileOffsets)
                {
                    var rid = writer.MetaData.GetRid(native);
                    writer.MetaData.TablesHeap.MethodTable[rid].RVA = (uint)codeChunk.RVA;
                }
            }
        }

        Action<uint[], uint[]> encryptFunc;

        public IEnumerable<Instruction> EmitDecrypt(MethodDef init, CEContext ctx, Local block, Local key)
        {
            StatementBlock encrypt, decrypt;
            ctx.DynCipher.GenerateCipherPair(ctx.Random, out encrypt, out decrypt);
            var ret = new List<Instruction>();

            var codeGen = new CipherCodeGen(block, key, init, ret);
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
            x86Encoding encoding = new x86Encoding();
            encoding.Compile(ctx);
            MutationHelper.ReplacePlaceholder(decoder, arg =>
            {
                List<Instruction> repl = new List<Instruction>();
                repl.AddRange(arg);
                repl.Add(Instruction.Create(OpCodes.Call, encoding.native));
                return repl.ToArray();
            });
            return encoding;
        }

        public uint Encode(object data, CEContext ctx, uint id)
        {
            var encoding = (x86Encoding)data;
            return (uint)encoding.expCompiled((int)id);
        }
    }
}
