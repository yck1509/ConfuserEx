using Confuser.Core;
using Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler {
    class TypeRewriter {

        private ConfuserContext context;
        private TypeService Service;

        private InstructionRewriterFactory RewriteFactory;


        public TypeRewriter(ConfuserContext _context) {
            context = _context;
            Service = context.Registry.GetService<TypeService>();

            RewriteFactory = new InstructionRewriterFactory() {
                new MethodSpecInstructionRewriter(),
                new MethodDefInstructionRewriter(),
                new MemberRefInstructionRewriter(),
                new TypeRefInstructionRewriter(),
                new TypeDefInstructionRewriter()
            };
        }

        public void ApplyGeterics() => Service.PrepairItems(); // Apply generics to sigs

        public void ImportCode(ModuleDef md) {
          //  ObjectCreationFactory.Import(md);
        }

        public void Process(MethodDef method) {

            var service = context.Registry.GetService<TypeService>();

            var il = method.Body.Instructions;

            for (int i = 0; i < il.Count; i++) {
                RewriteFactory.Process(service, method, il, i);
            }

        }

    }
}
