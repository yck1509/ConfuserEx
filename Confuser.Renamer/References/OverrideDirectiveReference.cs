using System;
using System.Linq;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class OverrideDirectiveReference : INameReference<MethodDef> {
		readonly VTableSlot baseSlot;
		readonly VTableSlot thisSlot;

		public OverrideDirectiveReference(VTableSlot thisSlot, VTableSlot baseSlot) {
			this.thisSlot = thisSlot;
			this.baseSlot = baseSlot;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			MethodDef method = thisSlot.MethodDef;

			IMethodDefOrRef target;
			if (baseSlot.MethodDefDeclType is GenericInstSig) {
				var declType = (GenericInstSig)baseSlot.MethodDefDeclType;
				target = new MemberRefUser(method.Module, baseSlot.MethodDef.Name, baseSlot.MethodDef.MethodSig, declType.ToTypeDefOrRef());
				target = (IMethodDefOrRef)new Importer(method.Module, ImporterOptions.TryToUseTypeDefs).Import(target);
			}
			else {
				target = baseSlot.MethodDef;
				if (target.Module != method.Module)
					target = (IMethodDefOrRef)new Importer(method.Module, ImporterOptions.TryToUseTypeDefs).Import(baseSlot.MethodDef);
			}
			if (target is MemberRef)
				service.AddReference(baseSlot.MethodDef, new MemberRefReference((MemberRef)target, baseSlot.MethodDef));

			if (method.Overrides.Any(impl =>
			                         new SigComparer().Equals(impl.MethodDeclaration.MethodSig, target.MethodSig) &&
			                         new SigComparer().Equals(impl.MethodDeclaration.DeclaringType.ResolveTypeDef(), target.DeclaringType.ResolveTypeDef())))
				return true;

			method.Overrides.Add(new MethodOverride(method, target));

			return true;
		}

		public bool ShouldCancelRename() {
			return false;
		}
	}
}