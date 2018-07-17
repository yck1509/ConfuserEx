using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
    class MemberRefAnalyzer : ContextAnalyzer<MemberRef> {
        public override void Process(ScannedMethod m, MemberRef o) {
            
            TypeSig sig = null;

            if (o.Class is TypeRef) {
                sig = (o.Class as TypeRef)?.ToTypeSig();
                
            }

            if (o.Class is TypeSpec) {
                sig = (o.Class as TypeSpec)?.ToTypeSig();
            }
            if (sig != null) {
                m.RegisterGeneric(sig);
            }
        }
    }
}
