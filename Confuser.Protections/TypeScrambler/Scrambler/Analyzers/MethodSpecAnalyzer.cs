using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
    class MethodSpecAnalyzer : ContextAnalyzer<MethodSpec> {
        public override void Process(ScannedMethod m, MethodSpec o) {

            foreach (var t in o.GenericInstMethodSig.GenericArguments) {
                m.RegisterGeneric(t);
            }
        }
    }
}
