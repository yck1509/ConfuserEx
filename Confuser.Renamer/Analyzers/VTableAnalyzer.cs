using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Renamer.References;
using dnlib.DotNet;

namespace Confuser.Renamer.Analyzers {
	internal class VTableAnalyzer : IRenamer {
		public void Analyze(ConfuserContext context, INameService service, IDnlibDef def) {
			VTable vTbl;

			if (def is TypeDef) {
				var type = (TypeDef)def;
				if (type.IsInterface)
					return;

				vTbl = service.GetVTables()[type];
				foreach (var ifaceVTbl in vTbl.InterfaceSlots.Values) {
					foreach (var slot in ifaceVTbl) {
						if (slot.Overrides == null)
							continue;
						Debug.Assert(slot.Overrides.MethodDef.DeclaringType.IsInterface);
						// A method in base type can implements an interface method for a
						// derived type. If the base type/interface is not in our control, we should
						// not rename the methods.
						bool baseUnderCtrl = context.Modules.Contains(slot.MethodDef.DeclaringType.Module as ModuleDefMD);
						bool ifaceUnderCtrl = context.Modules.Contains(slot.Overrides.MethodDef.DeclaringType.Module as ModuleDefMD);
						if (!baseUnderCtrl && ifaceUnderCtrl) {
							service.SetCanRename(slot.Overrides.MethodDef, false);
						}
						else if (baseUnderCtrl && !ifaceUnderCtrl) {
							service.SetCanRename(slot.MethodDef, false);
						}
					}
				}
			}
			else if (def is MethodDef) {
				var method = (MethodDef)def;
				if (!method.IsVirtual)
					return;

				vTbl = service.GetVTables()[method.DeclaringType];
				VTableSignature sig = VTableSignature.FromMethod(method);
				var slots = vTbl.FindSlots(method);

				if (!method.IsAbstract) {
					foreach (var slot in slots) {
						if (slot.Overrides == null)
							continue;
						// Better on safe side, add references to both methods.
						service.AddReference(method, new OverrideDirectiveReference(slot, slot.Overrides));
						service.AddReference(slot.Overrides.MethodDef, new OverrideDirectiveReference(slot, slot.Overrides));
					}
				}
			}
		}

		public void PreRename(ConfuserContext context, INameService service, IDnlibDef def) {
			//
		}

		public void PostRename(ConfuserContext context, INameService service, IDnlibDef def) {
			var method = def as MethodDef;
			if (method == null || !method.IsVirtual || method.Overrides.Count == 0)
				return;

			var methods = new HashSet<IMethodDefOrRef>(MethodDefOrRefComparer.Instance);
			method.Overrides
			      .RemoveWhere(impl => MethodDefOrRefComparer.Instance.Equals(impl.MethodDeclaration, method));
		}

		class MethodDefOrRefComparer : IEqualityComparer<IMethodDefOrRef> {
			public static readonly MethodDefOrRefComparer Instance = new MethodDefOrRefComparer();
			MethodDefOrRefComparer() { }

			public bool Equals(IMethodDefOrRef x, IMethodDefOrRef y) {
				return new SigComparer().Equals(x, y) && new SigComparer().Equals(x.DeclaringType, y.DeclaringType);
			}

			public int GetHashCode(IMethodDefOrRef obj) {
				return new SigComparer().GetHashCode(obj) * 5 + new SigComparer().GetHashCode(obj.DeclaringType);
			}
		}
	}
}