using System;
using System.Text;

namespace Confuser.Runtime {
	internal static class Constant {
		static byte[] b;

		static void Initialize() {
			var l = (uint)Mutation.KeyI0;
			uint[] q = Mutation.Placeholder(new uint[Mutation.KeyI0]);

			var k = new uint[0x10];
			var n = (uint)Mutation.KeyI1;
			for (int i = 0; i < 0x10; i++) {
				n ^= n >> 12;
				n ^= n << 25;
				n ^= n >> 27;
				k[i] = n;
			}

			int s = 0, d = 0;
			var w = new uint[0x10];
			var o = new byte[l * 4];
			while (s < l) {
				for (int j = 0; j < 0x10; j++)
					w[j] = q[s + j];
				Mutation.Crypt(w, k);
				for (int j = 0; j < 0x10; j++) {
					uint e = w[j];
					o[d++] = (byte)e;
					o[d++] = (byte)(e >> 8);
					o[d++] = (byte)(e >> 16);
					o[d++] = (byte)(e >> 24);
					k[j] ^= e;
				}
				s += 0x10;
			}

			b = Lzma.Decompress(o);
		}

		static T Get<T>(uint id) {
			id = (uint)Mutation.Placeholder((int)id);
			uint t = id >> 30;

			T ret = default(T);
			id &= 0x3fffffff;
			id <<= 2;

			if (t == Mutation.KeyI0) {
				int l = b[id++] | (b[id++] << 8) | (b[id++] << 16) | (b[id++] << 24);
				ret = (T)(object)string.Intern(Encoding.UTF8.GetString(b, (int)id, l));
			}
			// NOTE: Assume little-endian
			else if (t == Mutation.KeyI1) {
				var v = new T[1];
				Buffer.BlockCopy(b, (int)id, v, 0, Mutation.Value<int>());
				ret = v[0];
			}
			else if (t == Mutation.KeyI2) {
				int s = b[id++] | (b[id++] << 8) | (b[id++] << 16) | (b[id++] << 24);
				int l = b[id++] | (b[id++] << 8) | (b[id++] << 16) | (b[id++] << 24);
				Array v = Array.CreateInstance(typeof(T).GetElementType(), l);
				Buffer.BlockCopy(b, (int)id, v, 0, s - 4);
				ret = (T)(object)v;
			}
			return ret;
		}
	}
}