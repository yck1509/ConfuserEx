using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Services;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.DynCipher;
using Confuser.Renamer;

namespace Confuser.Protections.Constants
{
    class CEContext
    {
        public RandomGenerator Random;
        public ConfuserContext Context;
        public ModuleDef Module;
        public IMarkerService Marker;
        public IDynCipherService DynCipher;
        public INameService Name;

        public FieldDef BufferField;
        public FieldDef CacheField;
        public FieldDef DataField;
        public TypeDef DataType;
        public MethodDef InitMethod;

        public Mode Mode;
        public EncodeElements Elements;
        public int DecoderCount;

        public IEncodeMode ModeHandler;

        public List<uint> EncodedBuffer;
        public List<Tuple<MethodDef, DecoderDesc>> Decoders;
    }

    class DecoderDesc
    {
        public byte StringID;
        public byte NumberID;
        public byte InitializerID;
        public object Data;
    }
}
