using System;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal class AddKey : CryptoElement {
		public AddKey(int index)
			: base(0) {
			Index = index;
		}

		public int Index { get; private set; }

		public override void Initialize(RandomGenerator random) { }

		void EmitCore(CipherGenContext context) {
			Expression val = context.GetDataExpression(Index);

			context.Emit(new AssignmentStatement {
				Value = val ^ context.GetKeyExpression(Index),
				Target = val
			});
		}

		public override void Emit(CipherGenContext context) {
			EmitCore(context);
		}

		public override void EmitInverse(CipherGenContext context) {
			EmitCore(context);
		}
	}
}