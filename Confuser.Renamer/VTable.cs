using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using dnlib.DotNet;
using ILogger = Confuser.Core.ILogger;

namespace Confuser.Renamer {
	public class VTableSignature {
		internal VTableSignature(MethodSig sig, string name) {
			MethodSig = sig;
			Name = name;
		}

		public MethodSig MethodSig { get; private set; }
		public string Name { get; private set; }

		public static VTableSignature FromMethod(IMethod method) {
			MethodSig sig = method.MethodSig;
			TypeSig declType = method.DeclaringType.ToTypeSig();
			if (declType is GenericInstSig) {
				sig = GenericArgumentResolver.Resolve(sig, ((GenericInstSig)declType).GenericArguments);
			}
			return new VTableSignature(sig, method.Name);
		}

		public override bool Equals(object obj) {
			var other = obj as VTableSignature;
			if (other == null)
				return false;
			return new SigComparer().Equals(MethodSig, other.MethodSig) &&
			       Name.Equals(other.Name, StringComparison.Ordinal);
		}

		public override int GetHashCode() {
			int hash = 17;
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
			return FullNameCreator.MethodFullName("", Name, MethodSig);
		}
	}

	public class VTableSlot {
		internal VTableSlot(MethodDef def, TypeSig decl, VTableSignature signature)
			: this(def.DeclaringType.ToTypeSig(), def, decl, signature, null) { }

		internal VTableSlot(TypeSig defDeclType, MethodDef def, TypeSig decl, VTableSignature signature, VTableSlot overrides) {
			MethodDefDeclType = defDeclType;
			MethodDef = def;
			DeclaringType = decl;
			Signature = signature;
			Overrides = overrides;
		}

		// This is the type in which this slot is defined.
		public TypeSig DeclaringType { get; internal set; }
		// This is the signature of this slot.
		public VTableSignature Signature { get; internal set; }

		// This is the method that is currently in the slot.
		public TypeSig MethodDefDeclType { get; private set; }
		public MethodDef MethodDef { get; private set; }

		// This is the 'parent slot' that this slot overrides.
		public VTableSlot Overrides { get; private set; }

		public VTableSlot OverridedBy(MethodDef method) {
			return new VTableSlot(method.DeclaringType.ToTypeSig(), method, DeclaringType, Signature, this);
		}

		internal VTableSlot Clone() {
			return new VTableSlot(MethodDefDeclType, MethodDef, DeclaringType, Signature, Overrides);
		}

		public override string ToString() {
			return MethodDef.ToString();
		}
	}

	public class VTable {
		internal VTable(TypeSig type) {
			Type = type;
			Slots = new List<VTableSlot>();
			InterfaceSlots = new Dictionary<TypeSig, IList<VTableSlot>>();
		}

		public TypeSig Type { get; private set; }

		public IList<VTableSlot> Slots { get; private set; }
		public IDictionary<TypeSig, IList<VTableSlot>> InterfaceSlots { get; private set; }

		class VTableConstruction {
			class TypeSigComparer : IEqualityComparer<TypeSig> {
				public bool Equals(TypeSig x, TypeSig y) {
					return new SigComparer().Equals(x, y);
				}

				public int GetHashCode(TypeSig obj) {
					return new SigComparer().GetHashCode(obj);
				}

				public static readonly TypeSigComparer Instance = new TypeSigComparer();
			}

			// All virtual method slots, excluding interfaces
			public List<VTableSlot> AllSlots = new List<VTableSlot>();
			// All visible virtual method slots (i.e. excluded those being shadowed)
			public Dictionary<VTableSignature, VTableSlot> SlotsMap = new Dictionary<VTableSignature, VTableSlot>();
			public Dictionary<TypeSig, Dictionary<VTableSignature, VTableSlot>> InterfaceSlots = new Dictionary<TypeSig, Dictionary<VTableSignature, VTableSlot>>(TypeSigComparer.Instance);
		}

		public IEnumerable<VTableSlot> FindSlots(IMethod method) {
			return Slots
				.Concat(InterfaceSlots.SelectMany(iface => iface.Value))
				.Where(slot => slot.MethodDef == method);
		}

		public static VTable ConstructVTable(TypeDef typeDef, VTableStorage storage) {
			var ret = new VTable(typeDef.ToTypeSig());

			var virtualMethods = typeDef.Methods
			                            .Where(method => method.IsVirtual)
			                            .ToDictionary(
				                            method => VTableSignature.FromMethod(method),
				                            method => method
				);

			// See Partition II 12.2 for implementation algorithm
			VTableConstruction vTbl = new VTableConstruction();

			// Inherits base type's slots
			VTable baseVTbl = storage.GetVTable(typeDef.GetBaseTypeThrow());
			if (baseVTbl != null) {
				Inherits(vTbl, baseVTbl);
			}

			// Explicit interface implementation
			foreach (InterfaceImpl iface in typeDef.Interfaces) {
				VTable ifaceVTbl = storage.GetVTable(iface.Interface);
				if (ifaceVTbl != null) {
					Implements(vTbl, virtualMethods, ifaceVTbl, iface.Interface.ToTypeSig());
				}
			}

			// Normal interface implementation
			if (!typeDef.IsInterface) {
				// Interface methods cannot implements base interface methods.
				foreach (var iface in vTbl.InterfaceSlots.Values) {
					foreach (var entry in iface.ToList()) {
						if (!entry.Value.MethodDef.DeclaringType.IsInterface)
							continue;
						// This is the step 1 of 12.2 algorithm -- find implementation for still empty slots.
						// Note that it seems we should include newslot methods as well, despite what the standard said.
						MethodDef impl;
						VTableSlot implSlot;
						if (virtualMethods.TryGetValue(entry.Key, out impl))
							iface[entry.Key] = entry.Value.OverridedBy(impl);
						else if (vTbl.SlotsMap.TryGetValue(entry.Key, out implSlot))
							iface[entry.Key] = entry.Value.OverridedBy(implSlot.MethodDef);
					}
				}
			}

			// Normal overrides
			foreach (var method in virtualMethods) {
				VTableSlot slot;
				if (method.Value.IsNewSlot) {
					slot = new VTableSlot(method.Value, typeDef.ToTypeSig(), method.Key);
				}
				else {
					if (vTbl.SlotsMap.TryGetValue(method.Key, out slot)) {
						Debug.Assert(!slot.MethodDef.IsFinal);
						slot = slot.OverridedBy(method.Value);
					}
					else
						slot = new VTableSlot(method.Value, typeDef.ToTypeSig(), method.Key);
				}
				vTbl.SlotsMap[method.Key] = slot;
				vTbl.AllSlots.Add(slot);
			}

			// MethodImpls
			foreach (var method in virtualMethods) {
				foreach (var impl in method.Value.Overrides) {
					Debug.Assert(impl.MethodBody == method.Value);

					MethodDef targetMethod = impl.MethodDeclaration.ResolveThrow();
					if (targetMethod.DeclaringType.IsInterface) {
						var iface = impl.MethodDeclaration.DeclaringType.ToTypeSig();
						CheckKeyExist(storage, vTbl.InterfaceSlots, iface, "MethodImpl Iface");
						var ifaceVTbl = vTbl.InterfaceSlots[iface];

						var signature = VTableSignature.FromMethod(impl.MethodDeclaration);
						CheckKeyExist(storage, ifaceVTbl, signature, "MethodImpl Iface Sig");
						var targetSlot = ifaceVTbl[signature];

						// The Overrides of interface slots should directly points to the root interface slot
						while (targetSlot.Overrides != null)
							targetSlot = targetSlot.Overrides;
						Debug.Assert(targetSlot.MethodDef.DeclaringType.IsInterface);
						ifaceVTbl[targetSlot.Signature] = targetSlot.OverridedBy(method.Value);
					}
					else {
						var targetSlot = vTbl.AllSlots.Single(slot => slot.MethodDef == targetMethod);
						CheckKeyExist(storage, vTbl.SlotsMap, targetSlot.Signature, "MethodImpl Normal Sig");
						targetSlot = vTbl.SlotsMap[targetSlot.Signature]; // Use the most derived slot
						// Maybe implemented by above processes --- this process should take priority
						while (targetSlot.MethodDef.DeclaringType == typeDef)
							targetSlot = targetSlot.Overrides;
						vTbl.SlotsMap[targetSlot.Signature] = targetSlot.OverridedBy(method.Value);
					}
				}
			}

			// Populate result V-table
			ret.InterfaceSlots = vTbl.InterfaceSlots.ToDictionary(
				kvp => kvp.Key, kvp => (IList<VTableSlot>)kvp.Value.Values.ToList());

			foreach (var slot in vTbl.AllSlots) {
				ret.Slots.Add(slot);
			}

			return ret;
		}

		static void Implements(VTableConstruction vTbl, Dictionary<VTableSignature, MethodDef> virtualMethods, VTable ifaceVTbl, TypeSig iface) {
			// This is the step 2 of 12.2 algorithm -- use virtual newslot methods for explicit implementation.

			Func<VTableSlot, VTableSlot> implLookup = slot => {
				MethodDef impl;
				if (virtualMethods.TryGetValue(slot.Signature, out impl) &&
				    impl.IsNewSlot && !impl.DeclaringType.IsInterface) {
					// Interface methods cannot implements base interface methods.
					// The Overrides of interface slots should directly points to the root interface slot
					var targetSlot = slot;
					while (targetSlot.Overrides != null && !targetSlot.MethodDef.DeclaringType.IsInterface)
						targetSlot = targetSlot.Overrides;
					Debug.Assert(targetSlot.MethodDef.DeclaringType.IsInterface);
					return targetSlot.OverridedBy(impl);
				}
				return slot;
			};

			if (vTbl.InterfaceSlots.ContainsKey(iface)) {
				vTbl.InterfaceSlots[iface] = vTbl.InterfaceSlots[iface].Values.ToDictionary(
					slot => slot.Signature, implLookup);
			}
			else {
				vTbl.InterfaceSlots.Add(iface, ifaceVTbl.Slots.ToDictionary(
					slot => slot.Signature, implLookup));
			}

			foreach (var baseIface in ifaceVTbl.InterfaceSlots) {
				if (vTbl.InterfaceSlots.ContainsKey(baseIface.Key)) {
					vTbl.InterfaceSlots[baseIface.Key] = vTbl.InterfaceSlots[baseIface.Key].Values.ToDictionary(
						slot => slot.Signature, implLookup);
				}
				else {
					vTbl.InterfaceSlots.Add(baseIface.Key, baseIface.Value.ToDictionary(
						slot => slot.Signature, implLookup));
				}
			}
		}

		static void Inherits(VTableConstruction vTbl, VTable baseVTbl) {
			foreach (VTableSlot slot in baseVTbl.Slots) {
				vTbl.AllSlots.Add(slot);
				// It's possible to have same signature in multiple slots,
				// when a derived type shadow the base type using newslot.
				// In this case, use the derived type's slot in SlotsMap.

				// The derived type's slots are always at a later position 
				// than the base type, so it would naturally 'override'
				// their position in SlotsMap.
				vTbl.SlotsMap[slot.Signature] = slot;
			}

			// This is the step 1 of 12.2 algorithm -- copy the base interface implementation.
			foreach (var iface in baseVTbl.InterfaceSlots) {
				Debug.Assert(!vTbl.InterfaceSlots.ContainsKey(iface.Key));
				vTbl.InterfaceSlots.Add(iface.Key, iface.Value.ToDictionary(slot => slot.Signature, slot => slot));
			}
		}

		[Conditional("DEBUG")]
		static void CheckKeyExist<TKey, TValue>(VTableStorage storage, IDictionary<TKey, TValue> dictionary, TKey key, string name) {
			if (!dictionary.ContainsKey(key)) {
				storage.GetLogger().ErrorFormat("{0} not found: {1}", name, key);
				foreach (var k in dictionary.Keys)
					storage.GetLogger().ErrorFormat("    {0}", k);
			}
		}
	}

	public class VTableStorage {
		Dictionary<TypeDef, VTable> storage = new Dictionary<TypeDef, VTable>();
		ILogger logger;

		public VTableStorage(ILogger logger) {
			this.logger = logger;
		}

		public ILogger GetLogger() {
			return logger;
		}

		public VTable this[TypeDef type] {
			get { return storage.GetValueOrDefault(type, null); }
			internal set { storage[type] = value; }
		}

		VTable GetOrConstruct(TypeDef type) {
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
			if (type is TypeRef)
				return GetOrConstruct(((TypeRef)type).ResolveThrow());
			if (type is TypeSpec) {
				TypeSig sig = ((TypeSpec)type).TypeSig;
				if (sig is TypeDefOrRefSig) {
					TypeDef typeDef = ((TypeDefOrRefSig)sig).TypeDefOrRef.ResolveTypeDefThrow();
					return GetOrConstruct(typeDef);
				}
				if (sig is GenericInstSig) {
					var genInst = (GenericInstSig)sig;
					TypeDef openType = genInst.GenericType.TypeDefOrRef.ResolveTypeDefThrow();
					VTable vTable = GetOrConstruct(openType);

					return ResolveGenericArgument(openType, genInst, vTable);
				}
				throw new NotSupportedException("Unexpected type: " + type);
			}
			throw new UnreachableException();
		}

		static VTableSlot ResolveSlot(TypeDef openType, VTableSlot slot, IList<TypeSig> genArgs) {
			var newSig = GenericArgumentResolver.Resolve(slot.Signature.MethodSig, genArgs);
			TypeSig newDecl = slot.MethodDefDeclType;
			if (new SigComparer().Equals(newDecl, openType))
				newDecl = new GenericInstSig((ClassOrValueTypeSig)openType.ToTypeSig(), genArgs.ToArray());
			else
				newDecl = GenericArgumentResolver.Resolve(newDecl, genArgs);
			return new VTableSlot(newDecl, slot.MethodDef, slot.DeclaringType, new VTableSignature(newSig, slot.Signature.Name), slot.Overrides);
		}

		static VTable ResolveGenericArgument(TypeDef openType, GenericInstSig genInst, VTable vTable) {
			Debug.Assert(new SigComparer().Equals(openType, vTable.Type));
			var ret = new VTable(genInst);
			foreach (VTableSlot slot in vTable.Slots) {
				ret.Slots.Add(ResolveSlot(openType, slot, genInst.GenericArguments));
			}
			foreach (var iface in vTable.InterfaceSlots) {
				ret.InterfaceSlots.Add(GenericArgumentResolver.Resolve(iface.Key, genInst.GenericArguments),
				                       iface.Value.Select(slot => ResolveSlot(openType, slot, genInst.GenericArguments)).ToList());
			}
			return ret;
		}
	}
}