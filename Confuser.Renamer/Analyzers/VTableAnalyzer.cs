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
			VTableSlot slot = vTbl.FindSlot(method);
			Debug.Assert(slot != null);

			if (!method.IsAbstract) {
				foreach (VTableSlot baseSlot in slot.Overrides) {
					// Better on safe side, add references to both methods.
					service.AddReference(method, new OverrideDirectiveReference(slot, baseSlot));
					service.AddReference(baseSlot.MethodDef, new OverrideDirectiveReference(slot, baseSlot));
				}
			}
		}

		public void PreRename(ConfuserContext context, INameService service, IDnlibDef def) {
			var method = def as MethodDef;
			if (method == null || !method.IsVirtual)
				return;

			VTable vTbl = service.GetVTables()[method.DeclaringType];
			if (vTbl == null) // This case occurs at late injected types, like delegates
				return;
			VTableSignature sig = VTableSignature.FromMethod(method);
			VTableSlot slot = vTbl.FindSlot(method);
			Debug.Assert(slot != null);

			// Can't rename virtual methods which implement an interface method or override a method declared in a base type,
			// when the interface or base type is declared in an assembly that is not currently being processed
			if (slot.Overrides.Any(slotOverride => !context.Modules.Any(module => module.Assembly.FullName == slotOverride.MethodDef.DeclaringType.DefinitionAssembly.FullName)))
				service.SetCanRename(method, false);
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