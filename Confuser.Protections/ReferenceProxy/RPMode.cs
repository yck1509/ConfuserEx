using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Renamer.References;

namespace Confuser.Protections.ReferenceProxy
{
    abstract class RPMode
    {
        public abstract void ProcessCall(RPContext ctx, int instrIndex);
        public abstract void Finalize(RPContext ctx);

        static ITypeDefOrRef Import(RPContext ctx, TypeDef typeDef)
        {
            var retTypeRef = new Importer(ctx.Module, ImporterOptions.TryToUseTypeDefs).Import(typeDef);
            if (typeDef.Module != ctx.Module && ctx.Context.Modules.Contains((ModuleDefMD)typeDef.Module))
                ctx.Name.AddReference(typeDef, new TypeRefReference((TypeRef)retTypeRef, typeDef));
            return retTypeRef;
        }

        protected static MethodSig CreateProxySignature(RPContext ctx, IMethod method, bool newObj)
        {
            var module = ctx.Module;
            if (newObj)
            {
                Debug.Assert(method.MethodSig.HasThis);
                Debug.Assert(method.Name == ".ctor");
                TypeSig[] paramTypes = method.MethodSig.Params.Select(type =>
                {
                    if (ctx.TypeErasure && type.IsClassSig && method.MethodSig.HasThis)
                        return module.CorLibTypes.Object;
                    return type;
                }).ToArray();

                TypeSig retType;
                if (ctx.TypeErasure)    // newobj will not be used with value types
                    retType = module.CorLibTypes.Object;
                else
                {
                    var declType = method.DeclaringType.ResolveTypeDefThrow();
                    retType = Import(ctx, declType).ToTypeSig();
                }
                return MethodSig.CreateStatic(retType, paramTypes);
            }
            else
            {
                var paramTypes = method.MethodSig.Params.Select(type =>
                {
                    if (ctx.TypeErasure && type.IsClassSig && method.MethodSig.HasThis)
                        return module.CorLibTypes.Object;
                    return type;
                });
                if (method.MethodSig.HasThis && !method.MethodSig.ExplicitThis)
                {
                    var declType = method.DeclaringType.ResolveTypeDefThrow();
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

        protected static TypeDef GetDelegateType(RPContext ctx, MethodSig sig)
        {
            TypeDef ret;
            if (ctx.Delegates.TryGetValue(sig, out ret))
                return ret;

            ret = new TypeDefUser(ctx.Name.ObfuscateName(ctx.Method.DeclaringType.Namespace, Renamer.RenameMode.Unicode), ctx.Name.RandomName(), ctx.Module.CorLibTypes.GetTypeRef("System", "MulticastDelegate"));
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

            foreach (var def in ret.FindDefinitions())
            {
                ctx.Marker.Mark(def);
                ctx.Name.SetCanRename(def, false);
            }

            ctx.Delegates[sig] = ret;
            return ret;
        }
    }
}
