using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler {
    public class ScannedType : ScannedItem {
        public TypeDef TargetType { get; private set; }

        public ScannedType(TypeDef _t) {
            TargetType = _t;
        }

        public override void Scan() {
            foreach(var field in TargetType.Fields) {
                RegisterGeneric(field.FieldType);
            }
        }

        public override void PrepairGenerics() {
            foreach (var generic in Generics.Values) {
                TargetType.GenericParameters.Add(generic);
            }


            foreach (var field in TargetType.Fields) {
                field.FieldType = ConvertToGenericIfAvalible(field.FieldType);
            }
        }

        public override MDToken GetToken() => TargetType.MDToken;

        public override ClassOrValueTypeSig GetTarget() {
            return TargetType.TryGetClassOrValueTypeSig();
        }
    }
}
