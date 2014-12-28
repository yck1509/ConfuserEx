using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Renamer;
using Confuser.Renamer.References;
using dnlib.DotNet;

namespace Confuser.Protections.ReferenceProxy {
	internal abstract class RPMode {
		public abstract void ProcessCall(RPContext ctx, int instrIndex);
		public abstract void Finalize(RPContext ctx);

		static ITypeDefOrRef Import(RPContext ctx, TypeDef typeDef) {
			ITypeDefOrRef retTypeRef = new Importer(ctx.Module, ImporterOptions.TryToUseTypeDefs).Import(typeDef);
			if (typeDef.Module != ctx.Module && ctx.Context.Modules.Contains((ModuleDefMD)typeDef.Module))
				ctx.Name.AddReference(typeDef, new TypeRefReference((TypeRef)retTypeRef, typeDef));
			return retTypeRef;
		}

		protected static MethodSig CreateProxySignature(RPContext ctx, IMethod method, bool newObj) {
			ModuleDef module = ctx.Module;
			if (newObj) {
				Debug.Assert(method.MethodSig.HasThis);
				Debug.Assert(method.Name == ".ctor");
				TypeSig[] paramTypes = method.MethodSig.Params.Select(type => {
					if (ctx.TypeErasure && type.IsClassSig && method.MethodSig.HasThis)
						return module.CorLibTypes.Object;
					return type;
				}).ToArray();

				TypeSig retType;
				if (ctx.TypeErasure) // newobj will not be used with value types
					retType = module.CorLibTypes.Object;
				else {
					TypeDef declType = method.DeclaringType.ResolveTypeDefThrow();
					retType = Import(ctx, declType).ToTypeSig();
				}
				return MethodSig.CreateStatic(retType, paramTypes);
			}
			else {
				IEnumerable<TypeSig> paramTypes = method.MethodSig.Params.Select(type => {
					if (ctx.TypeErasure && type.IsClassSig && method.MethodSig.HasThis)
						return module.CorLibTypes.Object;
					return type;
				});
				if (method.MethodSig.HasThis && !method.MethodSig.ExplicitThis) {
					TypeDef declType = method.DeclaringType.ResolveTypeDefThrow();
					if (ctx.TypeErasure && !declType.IsValueType)
						paramTypes = new[] { module.CorLibTypes.Object }.Concat(paramTypes);
					else
						paramTypes = new[] { Import(ctx, declType).ToTypeSig() }.Concat(paramTypes);
				}
				TypeSig retType = method.MethodSig.RetType;
				if (ctx.TypeErasure && retType.IsClassSig)
					retType = module.CorLibTypes.Object;
				return MethodSig.CreateStatic(retType, paramTypes.ToArray());
			}
		}

		protected static TypeDef GetDelegateType(RPContext ctx, MethodSig sig) {
			TypeDef ret;
			if (ctx.Delegates.TryGetValue(sig, out ret))
				return ret;

			ret = new TypeDefUser(ctx.Name.ObfuscateName(ctx.Method.DeclaringType.Namespace, RenameMode.Unicode), ctx.Name.RandomName(), ctx.Module.CorLibTypes.GetTypeRef("System", "MulticastDelegate"));
			ret.Attributes = TypeAttributes.NotPublic | TypeAttributes.Sealed;

			var ctor = new MethodDefUser(".ctor", MethodSig.CreateInstance(ctx.Module.CorLibTypes.Void, ctx.Module.CorLibTypes.Object, ctx.Module.CorLibTypes.IntPtr));
			ctor.Attributes = MethodAttributes.Assembly | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName;
			ctor.ImplAttributes = MethodImplAttributes.Runtime;
			ret.Methods.Add(ctor);

			var invoke = new MethodDefUser("Invoke", sig.Clone());
			invoke.MethodSig.HasThis = true;
			invoke.Attributes = MethodAttributes.Assembly | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot;
			invoke.ImplAttributes = MethodImplAttributes.Runtime;
			ret.Methods.Add(invoke);

			ctx.Module.Types.Add(ret);

			foreach (IDnlibDef def in ret.FindDefinitions()) {
				ctx.Marker.Mark(def, ctx.Protection);
				ctx.Name.SetCanRename(def, false);
			}

			ctx.Delegates[sig] = ret;
			return ret;
		}
	}
}