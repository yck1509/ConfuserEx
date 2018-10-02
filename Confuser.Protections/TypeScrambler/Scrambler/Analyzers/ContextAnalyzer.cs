using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
    public abstract class ContextAnalyzer {

        public abstract Type TargetType();

        public abstract void ProcessOperand(ScannedMethod m, object o);
    }


    public abstract class ContextAnalyzer<T> : ContextAnalyzer {
        public override Type TargetType() => typeof(T);
        public abstract void Process(ScannedMethod m, T o);
        public override void ProcessOperand(ScannedMethod m, object o) {
            Process(m, (T)o);
        }

    }
}
