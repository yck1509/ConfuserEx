using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer;
using dnlib.DotNet;
using Confuser.Core.Helpers;
using Confuser.DynCipher;
using dnlib.DotNet.Emit;
using System.Diagnostics;

namespace Confuser.Protections.Constants
{
    class InjectPhase : ProtectionPhase
    {
        public InjectPhase(ConstantProtection parent)
            : base(parent)
        {
        }

        public override ProtectionTargets Targets
        {
            get { return ProtectionTargets.Methods; }
        }

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            if (parameters.Targets.Any())
            {
                var compression = context.Registry.GetService<ICompressionService>();
                var name = context.Registry.GetService<INameService>();
                var marker = context.Registry.GetService<IMarkerService>();
                var rt = context.Registry.GetService<IRuntimeService>();
                var moduleCtx = new CEContext()
                {
                    Random = context.Registry.GetService<IRandomService>().GetRandomGenerator(Parent.Id),
                    Context = context,
                    Module = context.CurrentModule,
                    Marker = marker,
                    DynCipher = context.Registry.GetService<IDynCipherService>(),
                    Name = name
                };

                // Extract parameters
                moduleCtx.Mode = parameters.GetParameter<Mode>(context, context.CurrentModule, "mode", Mode.Normal);
                moduleCtx.DecoderCount = parameters.GetParameter<int>(context, context.CurrentModule, "decoderCount", 5);

                switch (moduleCtx.Mode)
                {
                    case Mode.Normal:
                        moduleCtx.ModeHandler = new NormalMode();
                        break;
                    default:
                        throw new UnreachableException();
                }

                // Inject helpers
                var decomp = compression.GetRuntimeDecompressor(context.CurrentModule, member =>
                {
                    name.MarkHelper(member, marker);
                    if (member is MethodDef)
                        ProtectionParameters.GetParameters(context, (MethodDef)member).Remove(Parent);
                });
                InjectHelpers(context, compression, rt, moduleCtx);

                // Mutate codes
                MutateInitializer(moduleCtx, decomp);

                var cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
                cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, moduleCtx.InitMethod));

                context.Annotations.Set(context.CurrentModule, ConstantProtection.ContextKey, moduleCtx);
            }
        }

        void InjectHelpers(ConfuserContext context, ICompressionService compression, IRuntimeService rt, CEContext moduleCtx)
        {
            var members = InjectHelper.Inject(rt.GetRuntimeType("Confuser.Runtime.Constant"), context.CurrentModule.GlobalType, context.CurrentModule);
            foreach (var member in members)
            {
                if (member.Name == "Get")
                {
                    context.CurrentModule.GlobalType.Remove((MethodDef)member);
                    continue;
                }
                else if (member.Name == "b")
                    moduleCtx.BufferField = (FieldDef)member;
                else if (member.Name == "Initialize")
                    moduleCtx.InitMethod = (MethodDef)member;
                moduleCtx.Name.MarkHelper(member, moduleCtx.Marker);
            }
            ProtectionParameters.GetParameters(context, moduleCtx.InitMethod).Remove(Parent);

            var dataType = new TypeDefUser("", moduleCtx.Name.RandomName(), context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType"));
            dataType.Layout = TypeAttributes.ExplicitLayout;
            dataType.Visibility = TypeAttributes.NestedPrivate;
            dataType.IsSealed = true;
            moduleCtx.DataType = dataType;
            context.CurrentModule.GlobalType.NestedTypes.Add(dataType);
            moduleCtx.Name.MarkHelper(dataType, moduleCtx.Marker);

            moduleCtx.DataField = new FieldDefUser(moduleCtx.Name.RandomName(), new FieldSig(dataType.ToTypeSig()))
            {
                IsStatic = true,
                Access = FieldAttributes.CompilerControlled
            };
            context.CurrentModule.GlobalType.Fields.Add(moduleCtx.DataField);
            moduleCtx.Name.MarkHelper(moduleCtx.DataField, moduleCtx.Marker);

            var decoder = rt.GetRuntimeType("Confuser.Runtime.Constant").FindMethod("Get");
            moduleCtx.Decoders = new List<Tuple<MethodDef, DecoderDesc>>();
            for (int i = 0; i < moduleCtx.DecoderCount; i++)
            {
                var decoderInst = InjectHelper.Inject(decoder, context.CurrentModule);
                for (int j = 0; j < decoderInst.Body.Instructions.Count; j++)
                {
                    var instr = decoderInst.Body.Instructions[j];
                    IMethod method = instr.Operand as IMethod;
                    IField field = instr.Operand as IField;
                    if (instr.OpCode == OpCodes.Call &&
                        method.DeclaringType.Name == "Mutation" &&
                        method.Name == "Value")
                    {
                        decoderInst.Body.Instructions[j] = Instruction.Create(OpCodes.Sizeof, new GenericMVar(0).ToTypeDefOrRef());
                    }
                    else if (instr.OpCode == OpCodes.Ldsfld &&
                        method.DeclaringType.Name == "Constant")
                    {
                        if (field.Name == "b") instr.Operand = moduleCtx.BufferField;
                        else throw new UnreachableException();
                    }
                }
                context.CurrentModule.GlobalType.Methods.Add(decoderInst);
                moduleCtx.Name.MarkHelper(decoderInst, moduleCtx.Marker);
                ProtectionParameters.GetParameters(context, decoderInst).Remove(Parent);

                var decoderDesc = new DecoderDesc();

                decoderDesc.StringID = (byte)(moduleCtx.Random.NextByte() & 3);

                do decoderDesc.NumberID = (byte)(moduleCtx.Random.NextByte() & 3);
                while (decoderDesc.NumberID == decoderDesc.StringID);

                do decoderDesc.InitializerID = (byte)(moduleCtx.Random.NextByte() & 3);
                while (decoderDesc.InitializerID == decoderDesc.StringID || decoderDesc.InitializerID == decoderDesc.NumberID);

                MutationHelper.InjectKeys(decoderInst,
                    new int[] { 0, 1, 2 },
                    new int[] { decoderDesc.StringID, decoderDesc.NumberID, decoderDesc.InitializerID });
                decoderDesc.Data = moduleCtx.ModeHandler.CreateDecoder(decoderInst, moduleCtx);
                moduleCtx.Decoders.Add(Tuple.Create(decoderInst, decoderDesc));
            }
        }

        void MutateInitializer(CEContext moduleCtx, MethodDef decomp)
        {
            moduleCtx.InitMethod.Body.SimplifyMacros(moduleCtx.InitMethod.Parameters);
            List<Instruction> instrs = moduleCtx.InitMethod.Body.Instructions.ToList();
            for (int i = 0; i < instrs.Count; i++)
            {
                Instruction instr = instrs[i];
                IMethod method = instr.Operand as IMethod;
                if (instr.OpCode == OpCodes.Call)
                {
                    if (method.DeclaringType.Name == "Mutation" &&
                       method.Name == "Crypt")
                    {
                        Instruction ldBlock = instrs[i - 2];
                        Instruction ldKey = instrs[i - 1];
                        Debug.Assert(ldBlock.OpCode == OpCodes.Ldloc && ldKey.OpCode == OpCodes.Ldloc);
                        instrs.RemoveAt(i);
                        instrs.RemoveAt(i - 1);
                        instrs.RemoveAt(i - 2);
                        instrs.InsertRange(i - 2, moduleCtx.ModeHandler.EmitDecrypt(moduleCtx.InitMethod, moduleCtx, (Local)ldBlock.Operand, (Local)ldKey.Operand));
                    }
                    else if (method.DeclaringType.Name == "Mutation" &&
                       method.Name == "Value")
                    {
                        instr.OpCode = OpCodes.Ldtoken;
                        instr.Operand = moduleCtx.DataField;
                    }
                    else if (method.DeclaringType.Name == "Lzma" &&
                       method.Name == "Decompress")
                    {
                        instr.Operand = decomp;
                    }
                }
            }
            moduleCtx.InitMethod.Body.Instructions.Clear();
            foreach (var instr in instrs)
                moduleCtx.InitMethod.Body.Instructions.Add(instr);
        }
    }
}
