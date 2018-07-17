using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions {
    class MethodSpecInstructionRewriter : InstructionRewriter<MethodSpec> {
        public override void ProcessOperand(TypeService service, MethodDef method, IList<Instruction> body, ref int index, MethodSpec operand) {

            ScannedMethod t = service.GetItem(method.MDToken) as ScannedMethod;

            if (t != null) {

                var generics = operand.GenericInstMethodSig.GenericArguments.Select(x => t.ConvertToGenericIfAvalible(x));

                operand.GenericInstMethodSig = new GenericInstMethodSig(generics.ToArray());
            }
           
        }
    }
}
