// Common/CRC.cs

using System;

namespace SevenZip {
	internal class CRC {

		public static readonly uint[] Table;

		private uint _value = 0xFFFFFFFF;

		static CRC() {
			Table = new uint[256];
			const uint kPoly = 0xEDB88320;
			for (uint i = 0; i < 256; i++) {
				uint r = i;
				for (int j = 0; j < 8; j++)
					if ((r & 1) != 0)
						r = (r >> 1) ^ kPoly;
					else
						r >>= 1;
				Table[i] = r;
			}
		}

		public void Init() {
			_value = 0xFFFFFFFF;
		}

		public void UpdateByte(byte b) {
			_value = Table[(((byte)(_value)) ^ b)] ^ (_value >> 8);
		}

		public void Update(byte[] data, uint offset, uint size) {
			for (uint i = 0; i < size; i++)
				_value = Table[(((byte)(_value)) ^ data[offset + i])] ^ (_value >> 8);
		}

		public uint GetDigest() {
			return _value ^ 0xFFFFFFFF;
		}

		private static uint CalculateDigest(byte[] data, uint offset, uint size) {
			var crc = new CRC();
			// crc.Init();
			crc.Update(data, offset, size);
			return crc.GetDigest();
		}

		private static bool VerifyDigest(uint digest, byte[] data, uint offset, uint size) {
			return (CalculateDigest(data, offset, size) == digest);
		}

	}
}