using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer;
using dnlib.DotNet;

namespace Confuser.Protections.Constants {
	internal class CEContext {
		public FieldDef BufferField;
		public ConfuserContext Context;
		public FieldDef DataField;
		public TypeDef DataType;
		public int DecoderCount;
		public List<Tuple<MethodDef, DecoderDesc>> Decoders;
		public IDynCipherService DynCipher;
		public EncodeElements Elements;
		public List<uint> EncodedBuffer;
		public MethodDef InitMethod;
		public IMarkerService Marker;

		public Mode Mode;

		public IEncodeMode ModeHandler;
		public ModuleDef Module;
		public INameService Name;
		public RandomGenerator Random;
	}

	internal class DecoderDesc {
		public object Data;
		public byte InitializerID;
		public byte NumberID;
		public byte StringID;
	}
}