using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
    class MethodDefAnalyzer : ContextAnalyzer<MethodDef> {
        private TypeService service;

        public MethodDefAnalyzer(TypeService _service) {
            service = _service;
        }
        public override void Process(ScannedMethod m, MethodDef o) {
            var sc = service.GetItem(o.MDToken) as ScannedMethod;
            if(sc != null) {

                foreach (var regTypes in sc.TrueTypes) {
                    m.RegisterGeneric(regTypes);
                }
            }

        }
    }
}
