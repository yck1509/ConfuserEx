using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Services;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.DynCipher;
using Confuser.Renamer;

namespace Confuser.Protections.Resources
{
    class REContext
    {
        public RandomGenerator Random;
        public ConfuserContext Context;
        public ModuleDef Module;
        public IMarkerService Marker;
        public IDynCipherService DynCipher;
        public INameService Name;

        public FieldDef DataField;
        public TypeDef DataType;
        public MethodDef InitMethod;

        public Mode Mode;

        public IEncodeMode ModeHandler;
    }
}
