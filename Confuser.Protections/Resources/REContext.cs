using System;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Renamer;
using dnlib.DotNet;

namespace Confuser.Protections.Resources {
	internal class REContext {
		public ConfuserContext Context;

		public FieldDef DataField;
		public TypeDef DataType;
		public IDynCipherService DynCipher;
		public MethodDef InitMethod;
		public IMarkerService Marker;

		public Mode Mode;

		public IEncodeMode ModeHandler;
		public ModuleDef Module;
		public INameService Name;
		public RandomGenerator Random;
	}
}