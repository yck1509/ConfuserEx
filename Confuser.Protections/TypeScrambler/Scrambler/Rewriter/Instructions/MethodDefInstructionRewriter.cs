using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions
{
    class MethodDefInstructionRewriter : InstructionRewriter<MethodDef> {
        public override void ProcessOperand(TypeService service, MethodDef method, IList<Instruction> body, ref int index, MethodDef operand) {

            ScannedMethod tMethod = service.GetItem(operand.MDToken) as ScannedMethod;
			ScannedItem currentMethod = service.GetItem(method.MDToken) as ScannedMethod;

            if (tMethod != null) {
                
                var newspec = new MethodSpecUser(tMethod.TargetMethod, tMethod.CreateGenericMethodSig(currentMethod));
                
                body[index].Operand = newspec;
            }

           
        }
    }
}
