using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using MethodBody = dnlib.DotNet.Writer.MethodBody;

namespace Confuser.Protections.ReferenceProxy {
	internal class x86Encoding : IRPEncoding {
		readonly Dictionary<MethodDef, Tuple<MethodDef, Func<int, int>>> keys = new Dictionary<MethodDef, Tuple<MethodDef, Func<int, int>>>();
		readonly List<Tuple<MethodDef, byte[], MethodBody>> nativeCodes = new List<Tuple<MethodDef, byte[], MethodBody>>();
		bool addedHandler;

		public Instruction[] EmitDecode(MethodDef init, RPContext ctx, Instruction[] arg) {
			Tuple<MethodDef, Func<int, int>> key = GetKey(ctx, init);

			var repl = new List<Instruction>();
			repl.AddRange(arg);
			repl.Add(Instruction.Create(OpCodes.Call, key.Item1));
			return repl.ToArray();
		}

		public int Encode(MethodDef init, RPContext ctx, int value) {
			Tuple<MethodDef, Func<int, int>> key = GetKey(ctx, init);
			return key.Item2(value);
		}

		void Compile(RPContext ctx, out Func<int, int> expCompiled, out MethodDef native) {
			var var = new Variable("{VAR}");
			var result = new Variable("{RESULT}");

			CorLibTypeSig int32 = ctx.Module.CorLibTypes.Int32;
			native = new MethodDefUser(ctx.Context.Registry.GetService<INameService>().RandomName(), MethodSig.CreateStatic(int32, int32), MethodAttributes.PinvokeImpl | MethodAttributes.PrivateScope | MethodAttributes.Static);
			native.ImplAttributes = MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.PreserveSig;
			ctx.Module.GlobalType.Methods.Add(native);

			ctx.Context.Registry.GetService<IMarkerService>().Mark(native, ctx.Protection);
			ctx.Context.Registry.GetService<INameService>().SetCanRename(native, false);

			x86Register? reg;
			var codeGen = new x86CodeGen();
			Expression expression, inverse;
			do {
				ctx.DynCipher.GenerateExpressionPair(
					ctx.Random,
					new VariableExpression { Variable = var }, new VariableExpression { Variable = result },
					ctx.Depth, out expression, out inverse);

				reg = codeGen.GenerateX86(inverse, (v, r) => { return new[] { x86Instruction.Create(x86OpCode.POP, new x86RegisterOperand(r)) }; });
			} while (reg == null);

			byte[] code = CodeGenUtils.AssembleCode(codeGen, reg.Value);

			expCompiled = new DMCodeGen(typeof(int), new[] { Tuple.Create("{VAR}", typeof(int)) })
				.GenerateCIL(expression)
				.Compile<Func<int, int>>();

			nativeCodes.Add(Tuple.Create(native, code, (MethodBody)null));
			if (!addedHandler) {
				ctx.Context.CurrentModuleWriterListener.OnWriterEvent += InjectNativeCode;
				addedHandler = true;
			}
		}

		void InjectNativeCode(object sender, ModuleWriterListenerEventArgs e) {
			var writer = (ModuleWriterBase)sender;
			if (e.WriterEvent == ModuleWriterEvent.MDEndWriteMethodBodies) {
				for (int n = 0; n < nativeCodes.Count; n++)
					nativeCodes[n] = new Tuple<MethodDef, byte[], MethodBody>(
						nativeCodes[n].Item1,
						nativeCodes[n].Item2,
						writer.MethodBodies.Add(new MethodBody(nativeCodes[n].Item2)));
			}
			else if (e.WriterEvent == ModuleWriterEvent.EndCalculateRvasAndFileOffsets) {
				foreach (var native in nativeCodes) {
					uint rid = writer.MetaData.GetRid(native.Item1);
					writer.MetaData.TablesHeap.MethodTable[rid].RVA = (uint)native.Item3.RVA;
				}
			}
		}

		Tuple<MethodDef, Func<int, int>> GetKey(RPContext ctx, MethodDef init) {
			Tuple<MethodDef, Func<int, int>> ret;
			if (!keys.TryGetValue(init, out ret)) {
				Func<int, int> keyFunc;
				MethodDef native;
				Compile(ctx, out keyFunc, out native);
				keys[init] = ret = Tuple.Create(native, keyFunc);
			}
			return ret;
		}

		class CodeGen : CILCodeGen {
			readonly Instruction[] arg;

			public CodeGen(Instruction[] arg, MethodDef method, IList<Instruction> instrs)
				: base(method, instrs) {
				this.arg = arg;
			}

			protected override void LoadVar(Variable var) {
				if (var.Name == "{RESULT}") {
					foreach (Instruction instr in arg)
						Emit(instr);
				}
				else
					base.LoadVar(var);
			}
		}
	}
}