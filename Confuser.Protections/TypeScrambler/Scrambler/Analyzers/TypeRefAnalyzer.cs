using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
    class TypeRefAnalyzer : ContextAnalyzer<TypeRef> {
        public override void Process(ScannedMethod m, TypeRef o) {
            
            m.RegisterGeneric(o.ToTypeSig());
        }
    }
}
