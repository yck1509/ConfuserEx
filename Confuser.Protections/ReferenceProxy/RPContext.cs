using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Services;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.DynCipher;
using dnlib.DotNet.Emit;
using Confuser.Renamer;

namespace Confuser.Protections.ReferenceProxy
{
    enum Mode
    {
        Mild,
        Strong,
        Ftn
    }
    enum EncodingType
    {
        Normal,
        Expression,
        x86
    }
    class RPContext
    {
        public RandomGenerator Random;
        public ConfuserContext Context;
        public ModuleDef Module;
        public MethodDef Method;
        public HashSet<Instruction> BranchTargets;
        public CilBody Body;
        public IMarkerService Marker;
        public IDynCipherService DynCipher;
        public INameService Name;

        public Mode Mode;
        public EncodingType Encoding;
        public bool TypeErasure;
        public bool InternalAlso;

        public RPMode ModeHandler;
        public IRPEncoding EncodingHandler;

        public Dictionary<MethodSig, TypeDef> Delegates;
    }
}
