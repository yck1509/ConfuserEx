using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions {
    class TypeRefInstructionRewriter : InstructionRewriter<TypeRef> {
        public override void ProcessOperand(TypeService service, MethodDef method, IList<Instruction> body, ref int index, TypeRef operand) {
            ScannedItem current = service.GetItem(method.MDToken);
            if (current == null) {
                return;
            }

            body[index].Operand = new TypeSpecUser(current.ConvertToGenericIfAvalible(operand.ToTypeSig()));

        }
    }
}
