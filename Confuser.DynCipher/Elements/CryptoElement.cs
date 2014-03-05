using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Core.Services;

namespace Confuser.DynCipher.Elements
{
    abstract class CryptoElement
    {
        public int DataCount { get; private set; }
        public int[] DataIndexes { get; private set; }
        public CryptoElement(int count)
        {
            DataCount = count;
            DataIndexes = new int[count];
        }

        public abstract void Initialize(RandomGenerator random);
        public abstract void Emit(CipherGenContext context);
        public abstract void EmitInverse(CipherGenContext context);
    }
}
