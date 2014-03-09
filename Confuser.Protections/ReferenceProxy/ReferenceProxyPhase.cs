using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Confuser.Core.Services;
using Confuser.Renamer;
using Confuser.DynCipher;

namespace Confuser.Protections.ReferenceProxy
{
    class ReferenceProxyPhase : ProtectionPhase
    {
        public ReferenceProxyPhase(ReferenceProxyProtection parent)
            : base(parent)
        {
        }

        public override ProtectionTargets Targets
        {
            get { return ProtectionTargets.Methods; }
        }

        class RPStore
        {
            public RandomGenerator random;

            public MildMode mild;
            public StrongMode strong;

            public NormalEncoding normal;
            public ExpressionEncoding expression;
            public x86Encoding x86;

            class MethodSigComparer : IEqualityComparer<MethodSig>
            {
                public bool Equals(MethodSig x, MethodSig y)
                {
                    return new SigComparer().Equals(x, y);
                }

                public int GetHashCode(MethodSig obj)
                {
                    return new SigComparer().GetHashCode(obj);
                }
            }
            public Dictionary<MethodSig, TypeDef> delegates = new Dictionary<MethodSig, TypeDef>(new MethodSigComparer());
        }

        static RPContext ParseParameters(MethodDef method, ConfuserContext context, ProtectionParameters parameters, RPStore store)
        {
            RPContext ret = new RPContext();
            ret.Mode = parameters.GetParameter<Mode>(context, method, "mode", Mode.Mild);
            ret.Encoding = parameters.GetParameter<EncodingType>(context, method, "encoding", EncodingType.Normal);
            ret.InternalAlso = parameters.GetParameter<bool>(context, method, "internal", false);
            ret.TypeErasure = parameters.GetParameter<bool>(context, method, "typeErasure", false);
            ret.Depth = parameters.GetParameter<int>(context, method, "depth", 3);

            ret.Module = method.Module;
            ret.Method = method;
            ret.Body = method.Body;
            ret.BranchTargets = new HashSet<Instruction>(
                method.Body.Instructions
                .Select(instr => instr.Operand as Instruction)
                .Concat(method.Body.Instructions
                        .Where(instr => instr.Operand is Instruction[])
                        .SelectMany(instr => (Instruction[])instr.Operand))
                .Where(target => target != null));
                
            ret.Random = store.random;
            ret.Context = context;
            ret.Marker = context.Registry.GetService<IMarkerService>();
            ret.DynCipher = context.Registry.GetService<IDynCipherService>();
            ret.Name = context.Registry.GetService<INameService>();

            ret.Delegates = store.delegates;

            switch (ret.Mode)
            {
                case Mode.Mild:
                    ret.ModeHandler = store.mild ?? (store.mild = new MildMode());
                    break;
                case Mode.Strong:
                    ret.ModeHandler = store.strong ?? (store.strong = new StrongMode());
                    break;
                default:
                    throw new UnreachableException();
            }

            switch (ret.Encoding)
            {
                case EncodingType.Normal:
                    ret.EncodingHandler = store.normal ?? (store.normal = new NormalEncoding());
                    break;
                case EncodingType.Expression:
                    ret.EncodingHandler = store.expression ?? (store.expression = new ExpressionEncoding());
                    break;
                case EncodingType.x86:
                    ret.EncodingHandler = store.x86 ?? (store.x86 = new x86Encoding());
                    break;
                default:
                    throw new UnreachableException();
            }

            return ret;
        }

        static RPContext ParseParameters(ModuleDef module, ConfuserContext context, ProtectionParameters parameters, RPStore store)
        {
            RPContext ret = new RPContext();
            ret.Depth = parameters.GetParameter<int>(context, module, "depth", 3);
            ret.InitCount = parameters.GetParameter<int>(context, module, "initCount", 0x10);

            ret.Random = store.random;
            ret.Module = module;
            ret.Context = context;
            ret.Marker = context.Registry.GetService<IMarkerService>();
            ret.DynCipher = context.Registry.GetService<IDynCipherService>();
            ret.Name = context.Registry.GetService<INameService>();

            ret.Delegates = store.delegates;

            return ret;
        }

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            var random = context.Registry.GetService<IRandomService>().GetRandomGenerator(ReferenceProxyProtection._FullId);

            RPStore store = new RPStore() { random = random };

            foreach (var method in parameters.Targets.OfType<MethodDef>())
                if (method.HasBody && method.Body.Instructions.Count > 0)
                {
                    ProcessMethod(ParseParameters(method, context, parameters, store));
                }

            var ctx = ParseParameters(context.CurrentModule, context, parameters, store);

            if (store.mild != null)
                store.mild.Finalize(ctx);

            if (store.strong != null)
                store.strong.Finalize(ctx);
        }

        void ProcessMethod(RPContext ctx)
        {
            for (int i = 0; i < ctx.Body.Instructions.Count; i++)
            {
                var instr = ctx.Body.Instructions[i];
                if (instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt || instr.OpCode.Code == Code.Newobj)
                {
                    IMethod operand = (IMethod)instr.Operand;
                    // Call constructor
                    if (instr.OpCode.Code != Code.Newobj && operand.Name == ".ctor")
                        continue;
                    // Internal reference option
                    if (operand is MethodDef && !ctx.InternalAlso)
                        continue;
                    // No generic methods
                    if (operand is MethodSpec)
                        continue;
                    // No generic types / array types
                    if (operand.DeclaringType is TypeSpec)
                        continue;
                    var declType = operand.DeclaringType.ResolveTypeDefThrow();
                    // No delegates
                    if (declType.IsDelegate())
                        continue;
                    // No instance value type methods
                    if (declType.IsValueType && operand.MethodSig.HasThis)
                        return;
                    // No prefixed call
                    if (i - 1 >= 0 && ctx.Body.Instructions[i - 1].OpCode.OpCodeType == OpCodeType.Prefix)
                        continue;

                    ctx.ModeHandler.ProcessCall(ctx, i);
                }
            }
        }
    }
}
