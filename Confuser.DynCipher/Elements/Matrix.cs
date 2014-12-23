using System;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal class Matrix : CryptoElement {
		public Matrix()
			: base(4) { }

		public uint[,] Key { get; private set; }
		public uint[,] InverseKey { get; private set; }

		static uint[,] GenerateUnimodularMatrix(RandomGenerator random) {
			Func<uint> next = () => (uint)random.NextInt32(4);

			uint[,] l = {
				{ 1, 0, 0, 0 },
				{ next(), 1, 0, 0 },
				{ next(), next(), 1, 0 },
				{ next(), next(), next(), 1 }
			};
			uint[,] u = {
				{ 1, next(), next(), next() },
				{ 0, 1, next(), next() },
				{ 0, 0, 1, next() },
				{ 0, 0, 0, 1 }
			};

			return mul(l, u);
		}

		static uint[,] mul(uint[,] a, uint[,] b) {
			int n = a.GetLength(0), p = b.GetLength(1);
			int m = a.GetLength(1);
			if (b.GetLength(0) != m) return null;

			var ret = new uint[n, p];
			for (int i = 0; i < n; i++)
				for (int j = 0; j < p; j++) {
					ret[i, j] = 0;
					for (int k = 0; k < m; k++)
						ret[i, j] += a[i, k] * b[k, j];
				}
			return ret;
		}

		static uint cofactor4(uint[,] mat, int i, int j) {
			var sub = new uint[3, 3];
			for (int ci = 0, si = 0; ci < 4; ci++, si++) {
				if (ci == i) {
					si--;
					continue;
				}
				for (int cj = 0, sj = 0; cj < 4; cj++, sj++) {
					if (cj == j) {
						sj--;
						continue;
					}
					sub[si, sj] = mat[ci, cj];
				}
			}
			uint ret = det3(sub);
			if ((i + j) % 2 == 0) return ret;
			return (uint)(-ret);
		}

		static uint det3(uint[,] mat) {
			return mat[0, 0] * mat[1, 1] * mat[2, 2] +
			       mat[0, 1] * mat[1, 2] * mat[2, 0] +
			       mat[0, 2] * mat[1, 0] * mat[2, 1] -
			       mat[0, 2] * mat[1, 1] * mat[2, 0] -
			       mat[0, 1] * mat[1, 0] * mat[2, 2] -
			       mat[0, 0] * mat[1, 2] * mat[2, 1];
		}

		static uint[,] transpose4(uint[,] mat) {
			var ret = new uint[4, 4];
			for (int i = 0; i < 4; i++)
				for (int j = 0; j < 4; j++)
					ret[j, i] = mat[i, j];
			return ret;
		}

		public override void Initialize(RandomGenerator random) {
			InverseKey = mul(transpose4(GenerateUnimodularMatrix(random)), GenerateUnimodularMatrix(random));

			var cof = new uint[4, 4];
			for (int i = 0; i < 4; i++)
				for (int j = 0; j < 4; j++)
					cof[i, j] = cofactor4(InverseKey, i, j);
			Key = transpose4(cof);
		}

		void EmitCore(CipherGenContext context, uint[,] k) {
			Expression a = context.GetDataExpression(DataIndexes[0]);
			Expression b = context.GetDataExpression(DataIndexes[1]);
			Expression c = context.GetDataExpression(DataIndexes[2]);
			Expression d = context.GetDataExpression(DataIndexes[3]);

			VariableExpression ta, tb, tc, td;

			Func<uint, LiteralExpression> l = v => (LiteralExpression)v;
			using (context.AcquireTempVar(out ta))
			using (context.AcquireTempVar(out tb))
			using (context.AcquireTempVar(out tc))
			using (context.AcquireTempVar(out td)) {
				context.Emit(new AssignmentStatement {
					Value = a * l(k[0, 0]) + b * l(k[0, 1]) + c * l(k[0, 2]) + d * l(k[0, 3]),
					Target = ta
				}).Emit(new AssignmentStatement {
					Value = a * l(k[1, 0]) + b * l(k[1, 1]) + c * l(k[1, 2]) + d * l(k[1, 3]),
					Target = tb
				}).Emit(new AssignmentStatement {
					Value = a * l(k[2, 0]) + b * l(k[2, 1]) + c * l(k[2, 2]) + d * l(k[2, 3]),
					Target = tc
				}).Emit(new AssignmentStatement {
					Value = a * l(k[3, 0]) + b * l(k[3, 1]) + c * l(k[3, 2]) + d * l(k[3, 3]),
					Target = td
				})
				       .Emit(new AssignmentStatement { Value = ta, Target = a })
				       .Emit(new AssignmentStatement { Value = tb, Target = b })
				       .Emit(new AssignmentStatement { Value = tc, Target = c })
				       .Emit(new AssignmentStatement { Value = td, Target = d });
			}
		}

		public override void Emit(CipherGenContext context) {
			EmitCore(context, Key);
		}

		public override void EmitInverse(CipherGenContext context) {
			EmitCore(context, InverseKey);
		}
	}
}