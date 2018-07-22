using Confuser.Renamer;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler {
    public abstract class ScannedItem {

        internal Dictionary<uint, GenericParam> Generics = new Dictionary<uint, GenericParam>();
        public List<TypeSig> TrueTypes = new List<TypeSig>();
        public ushort GenericCount  {get;set;}
        public bool RegisterGeneric(TypeSig t) {
            if (t == null || t.ScopeType == null || t.IsSZArray) {
                return false;
            }

            if (!Generics.ContainsKey(t.ScopeType.MDToken.Raw)) {
                Generics.Add(t.ScopeType.MDToken.Raw, new GenericParamUser(GenericCount++, GenericParamAttributes.NoSpecialConstraint, "koi"));
                TrueTypes.Add(t);
                return true;
            } else {
                return false;
            }
            
        }

        public GenericMVar GetGeneric(TypeSig t) {
            GenericParam gp = null;

            if (t.ContainsGenericParameter) return null;
            if(t.ScopeType == null) return null;

            if (Generics.TryGetValue(t.ScopeType.MDToken.Raw, out gp))
            {
                return new GenericMVar(gp.Number);
            }
            else
            {
                return null;
            }
            
        }

        public TypeSig ConvertToGenericIfAvalible(TypeSig t) {

            TypeSig newSig = GetGeneric(t);
            if(newSig != null && t.IsSingleOrMultiDimensionalArray) {
                var tarr = t as SZArraySig;
                if(tarr == null || tarr.IsMultiDimensional) {
                    newSig = null;
                } else {
                    newSig = new ArraySig(newSig, tarr.Rank);
                }
            }
            return newSig ?? t;
        }

        public GenericInstMethodSig CreateGenericMethodSig(ScannedItem from) {
            if (from == null) {
                return new GenericInstMethodSig(TrueTypes);
            } else {
                TypeSig[] types = TrueTypes.Select(t => from.ConvertToGenericIfAvalible(t)).ToArray();
                return new GenericInstMethodSig(types);
            }

        }

        public GenericInstSig CreateGenericTypeSig(ScannedItem from) {
            return new GenericInstSig(GetTarget(), TrueTypes.Count);
            if (from == null) {
                return new GenericInstSig(GetTarget(), TrueTypes.ToArray());
            } else {
                TypeSig[] types = TrueTypes.Select(t => from.ConvertToGenericIfAvalible(t)).ToArray();
                return new GenericInstSig(GetTarget(), types);
            }

        }

        public abstract void PrepairGenerics();
        public abstract MDToken GetToken();
        
        public abstract void Scan();
        public abstract ClassOrValueTypeSig GetTarget();
    }
}
