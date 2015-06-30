using System;
using Confuser.Core;
using Confuser.Renamer.References;
using dnlib.DotNet;
using dnlib.DotNet.MD;

namespace Confuser.Renamer.Analyzers {
	internal class InterReferenceAnalyzer : IRenamer {
		// i.e. Inter-Assembly References, e.g. InternalVisibleToAttributes

		public void Analyze(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			var module = def as ModuleDefMD;
			if (module == null) return;

			MDTable table;
			uint len;

			// MemberRef/MethodSpec
			table = module.TablesStream.Get(Table.Method);
			len = table.Rows;
			for (uint i = 1; i <= len; i++) {
				MethodDef methodDef = module.ResolveMethod(i);
				foreach (var ov in methodDef.Overrides) {
					ProcessMemberRef(context, service, module, ov.MethodBody);
					ProcessMemberRef(context, service, module, ov.MethodDeclaration);
				}

				if (!methodDef.HasBody)
					continue;
				foreach (var instr in methodDef.Body.Instructions) {
					if (instr.Operand is MemberRef || instr.Operand is MethodSpec)
						ProcessMemberRef(context, service, module, (IMemberRef)instr.Operand);
				}
			}

			// TypeRef
			table = module.TablesStream.Get(Table.TypeRef);
			len = table.Rows;
			for (uint i = 1; i <= len; i++) {
				TypeRef typeRef = module.ResolveTypeRef(i);

				TypeDef typeDef = typeRef.ResolveTypeDefThrow();
				if (typeDef.Module != module && context.Modules.Contains((ModuleDefMD)typeDef.Module)) {
					service.AddReference(typeDef, new TypeRefReference(typeRef, typeDef));
				}
			}
		}

		void ProcessMemberRef(ConfuserContext context, INameService service, ModuleDefMD module, IMemberRef r) {
			var memberRef = r as MemberRef;
			if (r is MethodSpec)
				memberRef = ((MethodSpec)r).Method as MemberRef;

			if (memberRef != null) {
				if (memberRef.DeclaringType.TryGetArraySig() != null)
					return;

				TypeDef declType = memberRef.DeclaringType.ResolveTypeDefThrow();
				if (declType.Module != module && context.Modules.Contains((ModuleDefMD)declType.Module)) {
					var memberDef = (IDnlibDef)declType.ResolveThrow(memberRef);
					service.AddReference(memberDef, new MemberRefReference(memberRef, memberDef));
				}
			}
		}

		public void PreRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}

		public void PostRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}
	}
}