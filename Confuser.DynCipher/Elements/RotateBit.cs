using System;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal class RotateBit : CryptoElement {
		public RotateBit()
			: base(1) { }

		public int Bits { get; private set; }
		public bool IsAlternate { get; private set; }

		public override void Initialize(RandomGenerator random) {
			Bits = random.NextInt32(1, 32);
			IsAlternate = (random.NextInt32() % 2 == 0);
		}

		public override void Emit(CipherGenContext context) {
			Expression val = context.GetDataExpression(DataIndexes[0]);
			VariableExpression tmp;
			using (context.AcquireTempVar(out tmp)) {
				if (IsAlternate)
					context.Emit(new AssignmentStatement {
						Value = (val >> (32 - Bits)),
						Target = tmp
					}).Emit(new AssignmentStatement {
						Value = (val << Bits) | tmp,
						Target = val
					});
				else
					context.Emit(new AssignmentStatement {
						Value = (val << (32 - Bits)),
						Target = tmp
					}).Emit(new AssignmentStatement {
						Value = (val >> Bits) | tmp,
						Target = val
					});
			}
		}

		public override void EmitInverse(CipherGenContext context) {
			Expression val = context.GetDataExpression(DataIndexes[0]);
			VariableExpression tmp;
			using (context.AcquireTempVar(out tmp)) {
				if (IsAlternate)
					context.Emit(new AssignmentStatement {
						Value = (val << (32 - Bits)),
						Target = tmp
					}).Emit(new AssignmentStatement {
						Value = (val >> Bits) | tmp,
						Target = val
					});
				else
					context.Emit(new AssignmentStatement {
						Value = (val >> (32 - Bits)),
						Target = tmp
					}).Emit(new AssignmentStatement {
						Value = (val << Bits) | tmp,
						Target = val
					});
			}
		}
	}
}