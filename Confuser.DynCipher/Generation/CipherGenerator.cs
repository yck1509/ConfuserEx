using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Elements;
using Confuser.DynCipher.Transforms;

namespace Confuser.DynCipher.Generation {
	internal class CipherGenerator {
		const int MAT_RATIO = 4;
		const int NUMOP_RATIO = 10;
		const int SWAP_RATIO = 6;
		const int BINOP_RATIO = 9;
		const int ROTATE_RATIO = 6;
		const int RATIO_SUM = MAT_RATIO + NUMOP_RATIO + SWAP_RATIO + BINOP_RATIO + ROTATE_RATIO;
		const double VARIANCE = 0.2;


		static void Shuffle<T>(RandomGenerator random, IList<T> arr) {
			for (int i = 1; i < arr.Count; i++) {
				int j = random.NextInt32(i + 1);
				T tmp = arr[i];
				arr[i] = arr[j];
				arr[j] = tmp;
			}
		}

		static void PostProcessStatements(StatementBlock block, RandomGenerator random) {
			MulToShiftTransform.Run(block);
			NormalizeBinOpTransform.Run(block);
			ExpansionTransform.Run(block);
			ShuffleTransform.Run(block, random);
			ConvertVariables.Run(block);
		}

		public static void GeneratePair(RandomGenerator random, out StatementBlock encrypt, out StatementBlock decrypt) {
			double varPrecentage = 1 + ((random.NextDouble() * 2) - 1) * VARIANCE;
			var totalElements = (int)(((random.NextDouble() + 1) * RATIO_SUM) * varPrecentage);

			var elems = new List<CryptoElement>();
			for (int i = 0; i < totalElements * MAT_RATIO / RATIO_SUM; i++)
				elems.Add(new Matrix());
			for (int i = 0; i < totalElements * NUMOP_RATIO / RATIO_SUM; i++)
				elems.Add(new NumOp());
			for (int i = 0; i < totalElements * SWAP_RATIO / RATIO_SUM; i++)
				elems.Add(new Swap());
			for (int i = 0; i < totalElements * BINOP_RATIO / RATIO_SUM; i++)
				elems.Add(new BinOp());
			for (int i = 0; i < totalElements * ROTATE_RATIO / RATIO_SUM; i++)
				elems.Add(new RotateBit());
			for (int i = 0; i < 16; i++)
				elems.Add(new AddKey(i));
			Shuffle(random, elems);


			int[] x = Enumerable.Range(0, 16).ToArray();
			int index = 16;
			bool overdue = false;
			foreach (CryptoElement elem in elems) {
				elem.Initialize(random);
				for (int i = 0; i < elem.DataCount; i++) {
					if (index == 16) {
						overdue = true; // Can't shuffle now to prevent duplication
						index = 0;
					}
					elem.DataIndexes[i] = x[index++];
				}
				if (overdue) {
					Shuffle(random, x);
					index = 0;
					overdue = false;
				}
			}

			var encryptContext = new CipherGenContext(random, 16);
			foreach (CryptoElement elem in elems)
				elem.Emit(encryptContext);
			encrypt = encryptContext.Block;
			PostProcessStatements(encrypt, random);


			var decryptContext = new CipherGenContext(random, 16);
			foreach (CryptoElement elem in Enumerable.Reverse(elems))
				elem.EmitInverse(decryptContext);
			decrypt = decryptContext.Block;
			PostProcessStatements(decrypt, random);
		}
	}
}