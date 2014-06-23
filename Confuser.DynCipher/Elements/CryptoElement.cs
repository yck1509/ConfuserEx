using System;
using Confuser.Core.Services;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal abstract class CryptoElement {
		public CryptoElement(int count) {
			DataCount = count;
			DataIndexes = new int[count];
		}

		public int DataCount { get; private set; }
		public int[] DataIndexes { get; private set; }

		public abstract void Initialize(RandomGenerator random);
		public abstract void Emit(CipherGenContext context);
		public abstract void EmitInverse(CipherGenContext context);
	}
}