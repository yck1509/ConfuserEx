using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;
using System.Diagnostics;

namespace Confuser.Renamer
{
    public class VTableSignature
    {
        internal VTableSignature(MethodSig sig, string name)
        {
            this.MethodSig = sig;
            this.Name = name;
        }

        public MethodSig MethodSig { get; private set; }
        public string Name { get; private set; }

        public static VTableSignature FromMethod(IMethod method)
        {
            MethodSig sig = method.MethodSig;
            if (method.DeclaringType is TypeSpec)
            {
                var typeSpec = (TypeSpec)method.DeclaringType;
                if (typeSpec.TypeSig is GenericInstSig)
                {
                    sig = GenericArgumentResolver.Resolve(sig, ((GenericInstSig)typeSpec.TypeSig).GenericArguments);
                }
            }
            return new VTableSignature(sig, method.Name);
        }

        public override bool Equals(object obj)
        {
            VTableSignature other = obj as VTableSignature;
            if (other == null)
                return false;
            return new SigComparer().Equals(this.MethodSig, other.MethodSig) && this.Name.Equals(other.Name, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            int mh = new SigComparer().GetHashCode(MethodSig);
            return ((mh << 5) + mh) + Name.GetHashCode();
        }

        public static bool operator ==(VTableSignature a, VTableSignature b)
        {
            if (object.ReferenceEquals(a, b))
                return true;
            if (!object.Equals(a, null) && object.Equals(b, null))
                return false;

            return a.Equals(b);
        }
        public static bool operator !=(VTableSignature a, VTableSignature b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            return FullNameCreator.MethodFullName("", Name, MethodSig);
        }
    }

    public class VTableSlot
    {
        internal VTableSlot(VTable vTable, MethodDef def, TypeSig decl, VTableSignature signature)
        {
            this.VTable = vTable;
            this.MethodDef = def;
            this.DeclaringType = decl;
            this.Signature = signature;
            Overrides = new List<VTableSlot>();
        }

        public VTable VTable { get; private set; }
        public MethodDef MethodDef { get; private set; }

        public TypeSig DeclaringType { get; internal set; }
        public VTableSignature Signature { get; internal set; }

        public IList<VTableSlot> Overrides { get; private set; }
        public VTableSlot Override(VTableSlot slot)
        {
            this.Overrides.Add(slot);
            return this;
        }

        public override string ToString()
        {
            return MethodDef.ToString();
        }
    }

    public class VTable
    {
        internal VTable(TypeDef typeDef)
        {
            Type = typeDef;
            GenericArguments = null;
            Slots = new List<VTableSlot>();
            Finals = new List<VTableSlot>();
        }

        public TypeDef Type { get; private set; }
        public IList<TypeSig> GenericArguments { get; internal set; }
        public IList<VTableSlot> Slots { get; private set; }
        public IList<VTableSlot> Finals { get; private set; }

        public VTableSlot FindSlot(IMethod method)
        {
            return Slots.Concat(Finals).SingleOrDefault(slot => slot.MethodDef == method);
        }

        void Override(Dictionary<VTableSignature, List<VTableSlot>> slotDict, VTableSignature sig, VTableSlot slot)
        {
            List<VTableSlot> slotList = slotDict[sig];

            foreach (var baseSlot in slotList)
            {
                if (slot.MethodDef.IsReuseSlot || baseSlot.MethodDef.DeclaringType.IsInterface)
                    slot.Override(baseSlot);
                bool k = Slots.Remove(baseSlot);
                Debug.Assert(k);
            }
            slotList.Clear();
            if (!slot.MethodDef.IsFinal)
            {
                slotList.Add(slot);
                this.Slots.Add(slot);
            }
            else
                this.Finals.Add(slot);
        }
        void Override(Dictionary<VTableSignature, List<VTableSlot>> slotDict, VTableSignature sig, VTableSlot slot, MethodDef target)
        {
            List<VTableSlot> slotList = slotDict[sig];
            VTableSlot targetSlot = slotList.Single(baseSlot => baseSlot.MethodDef == target);

            if (slot.MethodDef.IsReuseSlot || targetSlot.MethodDef.DeclaringType.IsInterface)
                slot.Override(targetSlot);
            slotList.Remove(targetSlot);

            if (!slot.MethodDef.IsFinal)
            {
                slotDict.AddListEntry(slot.Signature, slot);
                this.Slots.Add(slot);
            }
            else
                this.Finals.Add(slot);
        }

        void Inherit(VTable parent, Dictionary<VTableSignature, List<VTableSlot>> slotDict)
        {
            foreach (VTableSlot slot in parent.Slots)
            {
                List<VTableSlot> slotList;
                if (slotDict.TryGetValue(slot.Signature, out slotList))
                {
                    if (slotList.Count > 0)
                    {
                        if (slotList.All(baseSlot => baseSlot.MethodDef.DeclaringType.IsInterface))
                        {
                            // Base slot is interface method => add together
                            if (!slotList.Any(baseSlot =>
                                    baseSlot.Signature == slot.Signature &&
                                    new SigComparer().Equals(baseSlot.DeclaringType, slot.DeclaringType)))
                            {
                                slotList.Add(slot);
                                this.Slots.Add(slot);
                            }
                        }
                        else
                            throw new UnreachableException();
                    }
                    else
                    {
                        slotList.Add(slot);
                        this.Slots.Add(slot);
                    }
                }
                else
                {
                    slotDict.AddListEntry(slot.Signature, slot);
                    this.Slots.Add(slot);
                }
            }
        }

        public static VTable ConstructVTable(TypeDef typeDef, VTableStorage storage)
        {
            VTable ret = new VTable(typeDef);

            var slotDict = new Dictionary<VTableSignature, List<VTableSlot>>();

            // Partition II 12.2

            // Interfaces
            foreach (var iface in typeDef.Interfaces)
            {
                VTable ifaceVTbl = storage.GetVTable(iface.Interface);
                if (ifaceVTbl != null)
                    ret.Inherit(ifaceVTbl, slotDict);
            }

            // Base type
            VTable baseVTbl = storage.GetVTable(typeDef.GetBaseTypeThrow());
            if (baseVTbl != null)
                ret.Inherit(baseVTbl, slotDict);

            List<MethodDef> virtualMethods = typeDef.Methods.Where(method => method.IsVirtual).ToList();
            HashSet<MethodDef> methodsProcessed = new HashSet<MethodDef>();


            // MethodImpls (Partition II 22.27)
            foreach (var method in virtualMethods)
                foreach (var impl in method.Overrides)
                {
                    Debug.Assert(impl.MethodBody == method);

                    MethodDef targetMethod = impl.MethodDeclaration.ResolveThrow();
                    VTableSignature sig = VTableSignature.FromMethod(impl.MethodDeclaration);
                    Debug.Assert(slotDict.ContainsKey(sig));

                    VTableSlot methodSlot = new VTableSlot(ret, method, method.DeclaringType.ToTypeSig(), VTableSignature.FromMethod(method));
                    ret.Override(slotDict, sig, methodSlot, targetMethod);
                    methodsProcessed.Add(method);
                }

            // Normal override
            foreach (var method in virtualMethods)
            {
                VTableSignature sig = VTableSignature.FromMethod(method);
                VTableSlot methodSlot = new VTableSlot(ret, method, method.DeclaringType.ToTypeSig(), sig);
                if (slotDict.ContainsKey(sig))
                {
                    ret.Override(slotDict, sig, methodSlot);
                    methodsProcessed.Add(method);
                }
            }

            // Remaining methods
            foreach (var method in typeDef.Methods.Where(method => method.IsVirtual).Except(methodsProcessed))
            {
                var slot = new VTableSlot(ret, method, method.DeclaringType.ToTypeSig(), VTableSignature.FromMethod(method));
                if (method.IsFinal)
                    ret.Finals.Add(slot);
                else
                {
                    Debug.Assert(!ret.Slots.Any(s => s.MethodDef == method));
                    ret.Slots.Add(slot);
                }
            }

            return ret;
        }
    }

    public class VTableStorage
    {
        Dictionary<TypeDef, VTable> storage = new Dictionary<TypeDef, VTable>();

        public VTable this[TypeDef type]
        {
            get { return storage.GetValueOrDefault(type, null); }
            internal set { storage[type] = value; }
        }

        VTable GetOrConstruct(TypeDef type)
        {
            VTable ret;
            if (!storage.TryGetValue(type, out ret))
                ret = storage[type] = VTable.ConstructVTable(type, this);
            return ret;
        }

        public VTable GetVTable(ITypeDefOrRef type)
        {
            if (type == null)
                return null;
            if (type is TypeDef)
                return GetOrConstruct((TypeDef)type);
            else if (type is TypeRef)
                return GetOrConstruct(((TypeRef)type).ResolveThrow());
            else if (type is TypeSpec)
            {
                TypeSig sig = ((TypeSpec)type).TypeSig;
                if (sig is TypeDefOrRefSig)
                {
                    var typeDef = ((TypeDefOrRefSig)sig).TypeDefOrRef.ResolveTypeDefThrow();
                    return GetOrConstruct(typeDef);
                }
                else if (sig is GenericInstSig)
                {
                    GenericInstSig genInst = (GenericInstSig)sig;
                    var openType = genInst.GenericType.TypeDefOrRef.ResolveTypeDefThrow();
                    var vTable = GetOrConstruct(openType);

                    return ResolveGenericArgument(openType, genInst, vTable);
                }
                else
                    throw new NotSupportedException("Unexpected type: " + type.ToString());
            }
            else
                throw new UnreachableException();
        }

        static VTable ResolveGenericArgument(TypeDef openType, GenericInstSig genInst, VTable vTable)
        {
            Debug.Assert(openType == vTable.Type);
            VTable ret = new VTable(openType);
            ret.GenericArguments = genInst.GenericArguments;
            foreach (var slot in vTable.Slots)
            {
                var newSig = GenericArgumentResolver.Resolve(slot.Signature.MethodSig, genInst.GenericArguments);
                var newDecl = slot.DeclaringType;
                if (new SigComparer().Equals(newDecl, openType))
                    newDecl = new GenericInstSig((ClassOrValueTypeSig)openType.ToTypeSig(), genInst.GenericArguments.ToArray());
                else
                    newDecl = GenericArgumentResolver.Resolve(newDecl, genInst.GenericArguments);
                ret.Slots.Add(new VTableSlot(ret, slot.MethodDef, newDecl, new VTableSignature(newSig, slot.Signature.Name)).Override(slot));
            }
            return ret;
        }
    }
}
