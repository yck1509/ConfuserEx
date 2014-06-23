using System;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal enum CryptoNumOps {
		Add,
		Mul,
		Xor,
		Xnor
	}

	internal class NumOp : CryptoElement {
		public NumOp()
			: base(1) { }

		public uint Key { get; private set; }
		public uint InverseKey { get; private set; }
		public CryptoNumOps Operation { get; private set; }

		public override void Initialize(RandomGenerator random) {
			Operation = (CryptoNumOps)(random.NextInt32(4));
			switch (Operation) {
				case CryptoNumOps.Add:
				case CryptoNumOps.Xor:
					Key = InverseKey = random.NextUInt32();
					break;
				case CryptoNumOps.Mul:
					Key = random.NextUInt32() | 1;
					InverseKey = MathsUtils.modInv(Key);
					break;
				case CryptoNumOps.Xnor:
					Key = random.NextUInt32();
					InverseKey = ~Key;
					break;
			}
		}

		public override void Emit(CipherGenContext context) {
			Expression val = context.GetDataExpression(DataIndexes[0]);
			switch (Operation) {
				case CryptoNumOps.Add:
					context.Emit(new AssignmentStatement {
						Value = val + (LiteralExpression)Key,
						Target = val
					});
					break;
				case CryptoNumOps.Xor:
					context.Emit(new AssignmentStatement {
						Value = val ^ (LiteralExpression)Key,
						Target = val
					});
					break;
				case CryptoNumOps.Mul:
					context.Emit(new AssignmentStatement {
						Value = val * (LiteralExpression)Key,
						Target = val
					});
					break;
				case CryptoNumOps.Xnor:
					context.Emit(new AssignmentStatement {
						Value = ~(val ^ (LiteralExpression)Key),
						Target = val
					});
					break;
			}
		}

		public override void EmitInverse(CipherGenContext context) {
			Expression val = context.GetDataExpression(DataIndexes[0]);
			switch (Operation) {
				case CryptoNumOps.Add:
					context.Emit(new AssignmentStatement {
						Value = val - (LiteralExpression)InverseKey,
						Target = val
					});
					break;
				case CryptoNumOps.Xor:
					context.Emit(new AssignmentStatement {
						Value = val ^ (LiteralExpression)InverseKey,
						Target = val
					});
					break;
				case CryptoNumOps.Mul:
					context.Emit(new AssignmentStatement {
						Value = val * (LiteralExpression)InverseKey,
						Target = val
					});
					break;
				case CryptoNumOps.Xnor:
					context.Emit(new AssignmentStatement {
						Value = val ^ (LiteralExpression)InverseKey,
						Target = val
					});
					break;
			}
		}
	}
}