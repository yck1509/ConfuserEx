using Confuser.Core;
using Confuser.Protections.TypeScramble.Scrambler;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble {
    class AnalyzePhase : ProtectionPhase {

        public AnalyzePhase(TypeScrambleProtection parent) : base(parent){
        }

        public override ProtectionTargets Targets => ProtectionTargets.Types | ProtectionTargets.Methods;

        public override string Name => "Type scanner";

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {

            //CreateGenericsForTypes(context, parameters.Targets.OfType<TypeDef>().WithProgress(context.Logger));

            CreateGenericsForMethods(context, parameters.Targets.OfType<MethodDef>()
                .OrderBy(x => 
                x?.Parameters?.Count ?? 0 + 
                x.Body?.Variables?.Count ?? 0)
                .WithProgress(context.Logger));
        }


        private void CreateGenericsForTypes(ConfuserContext context, IEnumerable<TypeDef> types) {
            TypeService service = context.Registry.GetService<TypeService>();

            foreach (TypeDef type in types) {
                if(type.Module.EntryPoint.DeclaringType != type) {
                    service.AddScannedItem(new ScannedType(type));
                    context.CheckCancellation();
                }
                
            }
        }

        private void CreateGenericsForMethods(ConfuserContext context, IEnumerable<MethodDef> methods) {
            TypeService service = context.Registry.GetService<TypeService>();

            foreach(MethodDef method in methods) {
                
                /*
                context.Logger.DebugFormat("[{0}]", method.Name);
                if (method.HasBody) {
                    foreach(var i in method.Body.Instructions) {
                        context.Logger.DebugFormat("{0} - {1} : {2}", i.OpCode, i?.Operand?.GetType().ToString() ?? "NULL", i.Operand);
                    }
                }*/
                

                if(method.Module.EntryPoint != method && !(method.HasOverrides || method.IsAbstract || method.IsConstructor || method.IsGetter) ) {
                    service.AddScannedItem(new ScannedMethod(service, method));
                    context.CheckCancellation();
                }
            }
        }

    }
}
