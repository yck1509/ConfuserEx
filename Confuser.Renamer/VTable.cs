using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer {
	public class VTableSignature {
		internal VTableSignature(TypeSig iface, MethodSig sig, string name) {
			if (iface.ScopeType.ResolveTypeDefThrow().IsInterface)
				InterfaceType = iface;
			else
				InterfaceType = null;
			MethodSig = sig;
			Name = name;
		}

		public TypeSig InterfaceType { get; private set; }
		public MethodSig MethodSig { get; private set; }
		public string Name { get; private set; }

		public static VTableSignature FromMethod(IMethod method) {
			MethodSig sig = method.MethodSig;
			TypeSig iface = method.DeclaringType.ToTypeSig();
			if (iface is GenericInstSig) {
				sig = GenericArgumentResolver.Resolve(sig, ((GenericInstSig)iface).GenericArguments);
			}
			return new VTableSignature(iface, sig, method.Name);
		}

		public override bool Equals(object obj) {
			var other = obj as VTableSignature;
			if (other == null)
				return false;
			return new SigComparer().Equals(InterfaceType, other.InterfaceType) && new SigComparer().Equals(MethodSig, other.MethodSig) && Name.Equals(other.Name, StringComparison.Ordinal);
		}

		public override int GetHashCode() {
			int hash = new SigComparer().GetHashCode(InterfaceType);
			hash = hash * 7 + new SigComparer().GetHashCode(MethodSig);
			return hash * 7 + Name.GetHashCode();
		}

		public static bool operator ==(VTableSignature a, VTableSignature b) {
			if (ReferenceEquals(a, b))
				return true;
			if (!Equals(a, null) && Equals(b, null))
				return false;

			return a.Equals(b);
		}

		public static bool operator !=(VTableSignature a, VTableSignature b) {
			return !(a == b);
		}

		public override string ToString() {
			return FullNameCreator.MethodFullName(InterfaceType == null ? "" : FullNameCreator.FullName(InterfaceType, false), Name, MethodSig);
		}
	}

	public class VTableSlot {
		internal VTableSlot(VTable vTable, MethodDef def, TypeSig decl, VTableSignature signature) {
			VTable = vTable;
			MethodDef = def;
			DeclaringType = decl;
			Signature = signature;
			Overrides = new List<VTableSlot>();
		}

		public VTable VTable { get; private set; }
		public MethodDef MethodDef { get; private set; }

		public TypeSig DeclaringType { get; internal set; }
		public VTableSignature Signature { get; internal set; }

		public IList<VTableSlot> Overrides { get; private set; }

		public VTableSlot Override(VTableSlot slot) {
			Overrides.Add(slot);
			return this;
		}

		public override string ToString() {
			return MethodDef.ToString();
		}
	}

	public class VTable {
		internal VTable(TypeDef typeDef) {
			Type = typeDef;
			GenericArguments = null;
			Slots = new List<VTableSlot>();
			Finals = new List<VTableSlot>();
		}

		public TypeDef Type { get; private set; }
		public IList<TypeSig> GenericArguments { get; internal set; }
		public IList<VTableSlot> Slots { get; private set; }
		public IList<VTableSlot> Finals { get; private set; }

		public VTableSlot FindSlot(IMethod method) {
			return Slots.Concat(Finals).SingleOrDefault(slot => slot.MethodDef == method);
		}

		private void Override(Dictionary<VTableSignature, List<VTableSlot>> slotDict, VTableSignature sig, VTableSlot slot) {
			List<VTableSlot> slotList = slotDict[sig];

			foreach (VTableSlot baseSlot in slotList) {
				if (slot.MethodDef.IsReuseSlot || baseSlot.MethodDef.DeclaringType.IsInterface)
					slot.Override(baseSlot);
				bool k = Slots.Remove(baseSlot);
				Debug.Assert(k);
			}
			slotList.Clear();
			if (!slot.MethodDef.IsFinal) {
				slotList.Add(slot);
				Slots.Add(slot);
			} else
				Finals.Add(slot);
		}

		private void Override(Dictionary<VTableSignature, List<VTableSlot>> slotDict, VTableSignature sig, VTableSlot slot, MethodDef target) {
			List<VTableSlot> slotList = slotDict[sig];
			VTableSlot targetSlot = slotList.Single(baseSlot => baseSlot.MethodDef == target);

			if (slot.MethodDef.IsReuseSlot || targetSlot.MethodDef.DeclaringType.IsInterface)
				slot.Override(targetSlot);
			slotList.Remove(targetSlot);

			if (!slot.MethodDef.IsFinal) {
				slotDict.AddListEntry(slot.Signature, slot);
				Slots.Add(slot);
			} else
				Finals.Add(slot);
		}

		private void Inherit(VTable parent, Dictionary<VTableSignature, List<VTableSlot>> slotDict) {
			foreach (VTableSlot slot in parent.Slots) {
				List<VTableSlot> slotList;
				if (slotDict.TryGetValue(slot.Signature, out slotList)) {
					if (slotList.Count > 0) {
						if (slotList.All(baseSlot => baseSlot.MethodDef.DeclaringType.IsInterface)) {
							// Base slot is interface method => add together
							if (!slotList.Any(baseSlot =>
							                  baseSlot.Signature == slot.Signature &&
							                  new SigComparer().Equals(baseSlot.DeclaringType, slot.DeclaringType))) {
								slotList.Add(slot);
								Slots.Add(slot);
							}
						} else
							throw new UnreachableException();
					} else {
						slotList.Add(slot);
						Slots.Add(slot);
					}
				} else {
					slotDict.AddListEntry(slot.Signature, slot);
					Slots.Add(slot);
				}
			}
		}

		public static VTable ConstructVTable(TypeDef typeDef, VTableStorage storage) {
			var ret = new VTable(typeDef);

			var slotDict = new Dictionary<VTableSignature, List<VTableSlot>>();

			// Partition II 12.2

			// Interfaces
			foreach (InterfaceImpl iface in typeDef.Interfaces) {
				VTable ifaceVTbl = storage.GetVTable(iface.Interface);
				if (ifaceVTbl != null)
					ret.Inherit(ifaceVTbl, slotDict);
			}

			// Base type
			VTable baseVTbl = storage.GetVTable(typeDef.GetBaseTypeThrow());
			if (baseVTbl != null)
				ret.Inherit(baseVTbl, slotDict);

			List<MethodDef> virtualMethods = typeDef.Methods.Where(method => method.IsVirtual).ToList();
			var methodsProcessed = new HashSet<MethodDef>();


			// MethodImpls (Partition II 22.27)
			foreach (MethodDef method in virtualMethods)
				foreach (MethodOverride impl in method.Overrides) {
					Debug.Assert(impl.MethodBody == method);

					MethodDef targetMethod = impl.MethodDeclaration.ResolveThrow();
					VTableSignature sig = VTableSignature.FromMethod(impl.MethodDeclaration);
					Debug.Assert(slotDict.ContainsKey(sig));

					var methodSlot = new VTableSlot(ret, method, method.DeclaringType.ToTypeSig(), VTableSignature.FromMethod(method));
					ret.Override(slotDict, sig, methodSlot, targetMethod);
					methodsProcessed.Add(method);
				}

			// Normal override
			foreach (MethodDef method in virtualMethods) {
				VTableSignature sig = VTableSignature.FromMethod(method);
				var methodSlot = new VTableSlot(ret, method, method.DeclaringType.ToTypeSig(), sig);
				if (slotDict.ContainsKey(sig) && slotDict[sig].Count > 0) {
					ret.Override(slotDict, sig, methodSlot);
					methodsProcessed.Add(method);
				}
			}

			// Remaining methods
			foreach (MethodDef method in typeDef.Methods.Where(method => method.IsVirtual).Except(methodsProcessed)) {
				var slot = new VTableSlot(ret, method, method.DeclaringType.ToTypeSig(), VTableSignature.FromMethod(method));
				if (method.IsFinal)
					ret.Finals.Add(slot);
				else {
					Debug.Assert(!ret.Slots.Any(s => s.MethodDef == method));
					ret.Slots.Add(slot);
				}
			}

			return ret;
		}
	}

	public class VTableStorage {
		private Dictionary<TypeDef, VTable> storage = new Dictionary<TypeDef, VTable>();

		public VTable this[TypeDef type] {
			get { return storage.GetValueOrDefault(type, null); }
			internal set { storage[type] = value; }
		}

		private VTable GetOrConstruct(TypeDef type) {
			VTable ret;
			if (!storage.TryGetValue(type, out ret))
				ret = storage[type] = VTable.ConstructVTable(type, this);
			return ret;
		}

		public VTable GetVTable(ITypeDefOrRef type) {
			if (type == null)
				return null;
			if (type is TypeDef)
				return GetOrConstruct((TypeDef)type);
			else if (type is TypeRef)
				return GetOrConstruct(((TypeRef)type).ResolveThrow());
			else if (type is TypeSpec) {
				TypeSig sig = ((TypeSpec)type).TypeSig;
				if (sig is TypeDefOrRefSig) {
					TypeDef typeDef = ((TypeDefOrRefSig)sig).TypeDefOrRef.ResolveTypeDefThrow();
					return GetOrConstruct(typeDef);
				} else if (sig is GenericInstSig) {
					var genInst = (GenericInstSig)sig;
					TypeDef openType = genInst.GenericType.TypeDefOrRef.ResolveTypeDefThrow();
					VTable vTable = GetOrConstruct(openType);

					return ResolveGenericArgument(openType, genInst, vTable);
				} else
					throw new NotSupportedException("Unexpected type: " + type.ToString());
			} else
				throw new UnreachableException();
		}

		private static VTable ResolveGenericArgument(TypeDef openType, GenericInstSig genInst, VTable vTable) {
			Debug.Assert(openType == vTable.Type);
			var ret = new VTable(openType);
			ret.GenericArguments = genInst.GenericArguments;
			foreach (VTableSlot slot in vTable.Slots) {
				MethodSig newSig = GenericArgumentResolver.Resolve(slot.Signature.MethodSig, genInst.GenericArguments);
				TypeSig newDecl = slot.DeclaringType;
				if (new SigComparer().Equals(newDecl, openType))
					newDecl = new GenericInstSig((ClassOrValueTypeSig)openType.ToTypeSig(), genInst.GenericArguments.ToArray());
				else
					newDecl = GenericArgumentResolver.Resolve(newDecl, genInst.GenericArguments);
				ret.Slots.Add(new VTableSlot(ret, slot.MethodDef, newDecl, new VTableSignature(genInst, newSig, slot.Signature.Name)).Override(slot));
			}
			return ret;
		}
	}
}