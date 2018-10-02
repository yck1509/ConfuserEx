using Confuser.Protections.TypeScramble.Scrambler.Analyzers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler {
    public class ScannedMethod : ScannedItem {

        public MethodDef TargetMethod { get; private set; }

        ContextAnalyzerFactory analyzers;

        public ScannedMethod(TypeService service, MethodDef target) {

            TargetMethod = target;

            GenericCount = (ushort)TargetMethod.GenericParameters.Count();

            analyzers = new ContextAnalyzerFactory(this) {
                new MemberRefAnalyzer(),
                new TypeRefAnalyzer(),
                new MethodSpecAnalyzer(),
                new MethodDefAnalyzer(service)
            };

        }

        public override void Scan() {

             if (TargetMethod.HasBody)
            {
                foreach (var v in TargetMethod.Body.Variables)
                {
                    RegisterGeneric(v.Type);
                }
            }
            
            if (TargetMethod.ReturnType != TargetMethod.Module.CorLibTypes.Void) {
                RegisterGeneric(TargetMethod.ReturnType);
            }
            foreach (var param in TargetMethod.Parameters) {
                if (param.Index == 0 && !TargetMethod.IsStatic) {
                    continue;
                }
                RegisterGeneric(param.Type);
            }

            if (TargetMethod.HasBody) {
                foreach (var i in TargetMethod.Body.Instructions) {
                    if(i.Operand != null) {
                        analyzers.Analyze(i.Operand);
                    }
                }
            }
        }

        public override void PrepairGenerics() {

            foreach (var generic in Generics.Values) {
                TargetMethod.GenericParameters.Add(generic);
            }

             if (TargetMethod.HasBody)
            {
                foreach (var v in TargetMethod.Body.Variables)
                {
                    v.Type = ConvertToGenericIfAvalible(v.Type);
                }
            }

            foreach (var p in TargetMethod.Parameters) {
                if (p.Index == 0 && !TargetMethod.IsStatic) {
                    continue;
                }
                p.Type = ConvertToGenericIfAvalible(p.Type);
                p.Name = string.Empty;
            }

            if (TargetMethod.ReturnType != TargetMethod.Module.CorLibTypes.Void) {
                TargetMethod.ReturnType = ConvertToGenericIfAvalible(TargetMethod.ReturnType);
            }

        }

        public override MDToken GetToken() {
            return TargetMethod.MDToken;
        }

        public override ClassOrValueTypeSig GetTarget() {
            return TargetMethod.DeclaringType.TryGetClassOrValueTypeSig();
        }
    }
}
