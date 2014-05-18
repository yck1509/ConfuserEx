using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using System.Diagnostics;
using Confuser.Renamer.References;

namespace Confuser.Renamer.Analyzers
{
    class TypeBlobAnalyzer : IRenamer
    {
        public void Analyze(ConfuserContext context, INameService service, IDnlibDef def)
        {
            ModuleDefMD module = def as ModuleDefMD;
            if (module == null) return;

            MDTable table;
            uint len;

            // MemberRef
            table = module.TablesStream.Get(Table.Method);
            len = table.Rows;
            var methods = Enumerable.Range(1, (int)len)
                .Select(rid => module.ResolveMethod((uint)rid));
            foreach (var method in methods)
            {
                foreach (var methodImpl in method.Overrides)
                {
                    if (methodImpl.MethodBody is MemberRef)
                        AnalyzeMemberRef(context, service, (MemberRef)methodImpl.MethodBody);
                    if (methodImpl.MethodDeclaration is MemberRef)
                        AnalyzeMemberRef(context, service, (MemberRef)methodImpl.MethodDeclaration);
                }
                if (!method.HasBody)
                    continue;
                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.Operand is MemberRef)
                        AnalyzeMemberRef(context, service, (MemberRef)instr.Operand);
                    else if (instr.Operand is MethodSpec)
                    {
                        MethodSpec spec = (MethodSpec)instr.Operand;
                        if (spec.Method is MemberRef)
                            AnalyzeMemberRef(context, service, (MemberRef)spec.Method);
                    }
                }
            }


            // CustomAttribute
            table = module.TablesStream.Get(Table.CustomAttribute);
            len = table.Rows;
            var attrs = Enumerable.Range(1, (int)len)
                .Select(rid => module.ResolveHasCustomAttribute(module.TablesStream.ReadCustomAttributeRow((uint)rid).Parent))
                .Distinct()
                .SelectMany(owner => owner.CustomAttributes);
            foreach (var attr in attrs)
            {
                if (attr.Constructor is MemberRef)
                    AnalyzeMemberRef(context, service, (MemberRef)attr.Constructor);

                foreach (var arg in attr.ConstructorArguments)
                    AnalyzeCAArgument(context, service, arg);

                foreach (var arg in attr.Fields)
                    AnalyzeCAArgument(context, service, arg.Argument);

                foreach (var arg in attr.Properties)
                    AnalyzeCAArgument(context, service, arg.Argument);

                TypeDef attrType = attr.AttributeType.ResolveTypeDefThrow();
                if (!context.Modules.Contains((ModuleDefMD)attrType.Module))
                    continue;

                foreach (var fieldArg in attr.Fields)
                {
                    FieldDef field = attrType.FindField(fieldArg.Name, new FieldSig(fieldArg.Type));
                    service.AddReference(field, new CAMemberReference(fieldArg, field));
                }
                foreach (var propertyArg in attr.Properties)
                {
                    PropertyDef property = attrType.FindProperty(propertyArg.Name, new PropertySig(true, propertyArg.Type));
                    service.AddReference(property, new CAMemberReference(propertyArg, property));
                }
            }
        }

        void AnalyzeCAArgument(ConfuserContext context, INameService service, CAArgument arg)
        {
            if (arg.Type.DefinitionAssembly.IsCorLib() && arg.Type.FullName == "System.Type")
            {
                TypeSig typeSig = (TypeSig)arg.Value;
                foreach (var typeRef in typeSig.FindTypeRefs())
                {
                    TypeDef typeDef = typeRef.ResolveTypeDefThrow();
                    if (context.Modules.Contains((ModuleDefMD)typeDef.Module))
                    {
                        if (typeRef is TypeRef)
                            service.AddReference(typeDef, new TypeRefReference((TypeRef)typeRef, typeDef));
                        service.ReduceRenameMode(typeDef, RenameMode.ASCII);
                    }
                }
            }
            else if (arg.Value is CAArgument[])
            {
                foreach (var elem in (CAArgument[])arg.Value)
                    AnalyzeCAArgument(context, service, elem);
            }
        }

        void AnalyzeMemberRef(ConfuserContext context, INameService service, MemberRef memberRef)
        {
            var declType = memberRef.DeclaringType;
            TypeSpec typeSpec = declType as TypeSpec;
            if (typeSpec == null)
                return;

            TypeSig sig = typeSpec.TypeSig;
            while (sig.Next != null)
                sig = sig.Next;


            Debug.Assert(sig is TypeDefOrRefSig || sig is GenericInstSig);
            if (sig is GenericInstSig)
            {
                GenericInstSig inst = (GenericInstSig)sig;
                Debug.Assert(!(inst.GenericType.TypeDefOrRef is TypeSpec));
                TypeDef openType = inst.GenericType.TypeDefOrRef.ResolveTypeDefThrow();
                if (!context.Modules.Contains((ModuleDefMD)openType.Module))
                    return;

                IDnlibDef member;
                if (memberRef.IsFieldRef) member = memberRef.ResolveFieldThrow();
                else if (memberRef.IsMethodRef) member = memberRef.ResolveMethodThrow();
                else throw new UnreachableException();

                service.AddReference(member, new MemberRefReference(memberRef, member));
            }

        }

        public void PreRename(ConfuserContext context, INameService service, IDnlibDef def)
        {
            //
        }

        public void PostRename(ConfuserContext context, INameService service, IDnlibDef def)
        {
            //
        }
    }
}
