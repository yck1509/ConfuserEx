using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions {
    class InstructionRewriterFactory : IEnumerable {

        private Dictionary<Type, InstructionRewriter> RewriterDefinitions = new Dictionary<Type, InstructionRewriter>();

        public void Add(InstructionRewriter i) {
            RewriterDefinitions.Add(i.TargetType(), i);
        }

        public void Process(TypeService service, MethodDef method, IList<Instruction> c, int index) {
            Instruction current = c[index];
            if(current.Operand == null) {
                return;
            }
            InstructionRewriter rw;
            if(RewriterDefinitions.TryGetValue(current.Operand.GetType().BaseType, out rw)) {
                rw.ProcessInstruction(service, method, c, ref index, current);
            }
        }

        public IEnumerator GetEnumerator() {
            return RewriterDefinitions.Values.GetEnumerator();
        }
    }
}
