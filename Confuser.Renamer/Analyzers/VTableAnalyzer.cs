using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			VTableSlot slot = vTbl.FindSlot(method);
			Debug.Assert(slot != null);

			if (method.IsAbstract) {
				service.SetCanRename(method, false);
			} else {
				foreach (VTableSlot baseSlot in slot.Overrides) {
					// Better on safe side, add references to both methods.
					service.AddReference(method, new OverrideDirectiveReference(slot, baseSlot));
					service.AddReference(baseSlot.MethodDef, new OverrideDirectiveReference(slot, baseSlot));
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