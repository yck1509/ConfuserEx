using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Renamer.References;
using dnlib.DotNet;

namespace Confuser.Renamer.Analyzers {
	internal class VTableAnalyzer : IRenamer {

		public void Analyze(ConfuserContext context, INameService service, IDnlibDef def) {
			var method = def as MethodDef;
			if (method == null || !method.IsVirtual)
				return;

			VTable vTbl = service.GetVTables()[method.DeclaringType];
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

		public void PreRename(ConfuserContext context, INameService service, IDnlibDef def) {
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
						// derived type. If the base type is not in our control, we should
						// not rename the interface method.
						if (!context.Modules.Contains(slot.MethodDef.DeclaringType.Module as ModuleDefMD) &&
							context.Modules.Contains(slot.Overrides.MethodDef.DeclaringType.Module as ModuleDefMD))
							service.SetCanRename(slot.Overrides.MethodDef, false);
					}
				}
			}
		}

		public void PostRename(ConfuserContext context, INameService service, IDnlibDef def) {
			var method = def as MethodDef;
			if (method == null || !method.IsVirtual || method.Overrides.Count == 0)
				return;

			var methods = new HashSet<IMethodDefOrRef>(MethodDefOrRefComparer.Instance);
			method.Overrides
				  .RemoveWhere(impl => MethodDefOrRefComparer.Instance.Equals(impl.MethodDeclaration, method));
		}

		private class MethodDefOrRefComparer : IEqualityComparer<IMethodDefOrRef> {

			public static readonly MethodDefOrRefComparer Instance = new MethodDefOrRefComparer();
			private MethodDefOrRefComparer() { }

			public bool Equals(IMethodDefOrRef x, IMethodDefOrRef y) {
				return new SigComparer().Equals(x, y) && new SigComparer().Equals(x.DeclaringType, y.DeclaringType);
			}

			public int GetHashCode(IMethodDefOrRef obj) {
				return new SigComparer().GetHashCode(obj) * 5 + new SigComparer().GetHashCode(obj.DeclaringType);
			}

		}

	}
}