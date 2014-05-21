using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Core.Services;
using dnlib.DotNet.Emit;

namespace Confuser.Renamer.Analyzers
{
    class LdtokenEnumAnalyzer : IRenamer
    {
        public void Analyze(ConfuserContext context, INameService service, IDnlibDef def)
        {
            MethodDef method = def as MethodDef;
            if (method == null || !method.HasBody)
                return;

            // When a ldtoken instruction reference a definition,
            // most likely it would be used in reflection and thus probably should not be renamed.
            // Also, when ToString is invoked on enum,
            // the enum should not be renamed.
            for (int i = 0; i < method.Body.Instructions.Count; i++)
            {
                var instr = method.Body.Instructions[i];
                if (instr.OpCode.Code == Code.Ldtoken)
                {
                    if (instr.Operand is MemberRef)
                    {
                        var member = ((MemberRef)instr.Operand).ResolveThrow();
                        if (context.Modules.Contains((ModuleDefMD)member.Module))
                            service.SetCanRename(member, false);
                    }
                    else if (instr.Operand is IField)
                    {
                        FieldDef field = ((IField)instr.Operand).ResolveThrow();
                        if (context.Modules.Contains((ModuleDefMD)field.Module))
                            service.SetCanRename(field, false);
                    }
                    else if (instr.Operand is IMethod)
                    {
                        MethodDef m = ((IMethod)instr.Operand).ResolveThrow();
                        if (context.Modules.Contains((ModuleDefMD)m.Module))
                            service.SetCanRename(method, false);
                    }
                    else if (instr.Operand is ITypeDefOrRef)
                    {
                        if (!(instr.Operand is TypeSpec))
                        {
                            TypeDef type = ((ITypeDefOrRef)instr.Operand).ResolveTypeDefThrow();
                            if (context.Modules.Contains((ModuleDefMD)type.Module) && 
                                HandleTypeOf(context, service, method, i))
                                DisableRename(service, type);
                        }
                    }
                    else
                        throw new UnreachableException();
                }
                else if ((instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt) &&
                    ((IMethod)instr.Operand).Name == "ToString")
                {
                    HandleEnum(context, service, method, i);
                }
            }
        }

        void HandleEnum(ConfuserContext context, INameService service, MethodDef method, int index)
        {
            IMethod target = (IMethod)method.Body.Instructions[index].Operand;
            if (target.FullName == "System.String System.Object::ToString()" ||
                target.FullName == "System.String System.Enum::ToString(System.String)")
            {
                int prevIndex = index - 1;
                while (prevIndex >= 0 && method.Body.Instructions[prevIndex].OpCode.Code == Code.Nop)
                    prevIndex--;

                if (prevIndex < 0)
                    return;

                var prevInstr = method.Body.Instructions[prevIndex];
                TypeSig targetType;

                if (prevInstr.Operand is MemberRef)
                {
                    MemberRef memberRef = (MemberRef)prevInstr.Operand;
                    targetType = memberRef.IsFieldRef ? memberRef.FieldSig.Type : memberRef.MethodSig.RetType;
                }
                else if (prevInstr.Operand is IField)
                    targetType = ((IField)prevInstr.Operand).FieldSig.Type;

                else if (prevInstr.Operand is IMethod)
                    targetType = ((IMethod)prevInstr.Operand).MethodSig.RetType;

                else if (prevInstr.Operand is ITypeDefOrRef)
                    targetType = ((ITypeDefOrRef)prevInstr.Operand).ToTypeSig();

                else if (prevInstr.GetParameter(method.Parameters) != null)
                    targetType = prevInstr.GetParameter(method.Parameters).Type;

                else if (prevInstr.GetLocal(method.Body.Variables) != null)
                    targetType = prevInstr.GetLocal(method.Body.Variables).Type;

                else
                    return;

                var targetTypeRef = targetType.ToBasicTypeDefOrRef();
                if (targetTypeRef == null)
                    return;

                var targetTypeDef = targetTypeRef.ResolveTypeDefThrow();
                if (targetTypeDef != null && targetTypeDef.IsEnum && context.Modules.Contains((ModuleDefMD)targetTypeDef.Module))
                    DisableRename(service, targetTypeDef);

            }
        }

        bool HandleTypeOf(ConfuserContext context, INameService service, MethodDef method, int index)
        {
            if (index + 1 >= method.Body.Instructions.Count)
                return true;

            IMethod gtfh = method.Body.Instructions[index + 1].Operand as IMethod;
            if (gtfh == null || gtfh.FullName != "System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)")
                return true;

            if (index + 2 < method.Body.Instructions.Count)
            {
                Instruction instr = method.Body.Instructions[index + 2];
                IMethod operand = instr.Operand as IMethod;
                if (instr.OpCode == OpCodes.Newobj && operand.FullName == "System.Void System.ComponentModel.ComponentResourceManager::.ctor(System.Type)")
                    return false;
                else if (instr.OpCode == OpCodes.Call)
                {
                    switch (operand.FullName)
                    {
                        case "System.Int32 System.Runtime.InteropServices.Marshal::SizeOf(System.Type)":
                        case "System.Object System.Runtime.InteropServices.Marshal::PtrToStructure(System.IntPtr,System.Type)":
                            return false;
                    }
                }
            }
            if (index + 3 < method.Body.Instructions.Count)
            {
                Instruction instr = method.Body.Instructions[index + 3];
                IMethod operand = instr.Operand as IMethod;
                if (instr.OpCode == OpCodes.Call)
                {
                    switch (operand.FullName)
                    {
                        case "System.IntPtr System.Runtime.InteropServices.Marshal::OffsetOf(System.Type,System.String)":
                            return false;
                    }
                }
            }

            return true;
        }

        void DisableRename(INameService service, TypeDef typeDef)
        {
            foreach (var m in typeDef.Methods)
                service.SetCanRename(m, false);

            foreach (var field in typeDef.Fields)
                service.SetCanRename(field, false);

            foreach (var prop in typeDef.Properties)
                service.SetCanRename(prop, false);

            foreach (var evt in typeDef.Events)
                service.SetCanRename(evt, false);

            foreach (var nested in typeDef.NestedTypes)
                DisableRename(service, nested);
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
