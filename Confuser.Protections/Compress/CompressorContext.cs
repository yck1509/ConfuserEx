using System;
using System.Collections.Generic;
using Confuser.Core.Services;
using dnlib.DotNet;

namespace Confuser.Protections.Compress {
	internal class CompressorContext {
		public AssemblyDef Assembly;
		public IKeyDeriver Deriver;
		public byte[] EncryptedModule;
		public MethodDef EntryPoint;
		public uint EntryPointToken;
		public byte[] KeySig;
		public uint KeyToken;
		public ModuleKind Kind;
		public List<Tuple<uint, uint, string>> ManifestResources;
		public int ModuleIndex;
		public string ModuleName;
		public byte[] OriginModule;
		public ModuleDef OriginModuleDef;
		public bool CompatMode;

		public byte[] Encrypt(ICompressionService compress, byte[] data, uint seed, Action<double> progressFunc) {
			data = (byte[])data.Clone();
			var dst = new uint[0x10];
			var src = new uint[0x10];
			ulong state = seed;
			for (int i = 0; i < 0x10; i++) {
				state = (state * state) % 0x143fc089;
				src[i] = (uint)state;
				dst[i] = (uint)((state * state) % 0x444d56fb);
			}
			uint[] key = Deriver.DeriveKey(dst, src);

			var z = (uint)(state % 0x8a5cb7);
			for (int i = 0; i < data.Length; i++) {
				data[i] ^= (byte)state;
				if ((i & 0xff) == 0)
					state = (state * state) % 0x8a5cb7;
			}
			data = compress.Compress(data, progressFunc);
			Array.Resize(ref data, (data.Length + 3) & ~3);

			var encryptedData = new byte[data.Length];
			int keyIndex = 0;
			for (int i = 0; i < data.Length; i += 4) {
				var datum = (uint)(data[i + 0] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
				uint encrypted = datum ^ key[keyIndex & 0xf];
				key[keyIndex & 0xf] = (key[keyIndex & 0xf] ^ datum) + 0x3ddb2819;
				encryptedData[i + 0] = (byte)(encrypted >> 0);
				encryptedData[i + 1] = (byte)(encrypted >> 8);
				encryptedData[i + 2] = (byte)(encrypted >> 16);
				encryptedData[i + 3] = (byte)(encrypted >> 24);
				keyIndex++;
			}

			return encryptedData;
		}
	}
}