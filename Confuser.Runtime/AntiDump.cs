using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Confuser.Runtime {
	internal static class AntiDump {
		[DllImport("kernel32.dll")]
		static extern unsafe bool VirtualProtect(byte* lpAddress, int dwSize, uint flNewProtect, out uint lpflOldProtect);

		static unsafe void Initialize() {
			uint old;
			Module module = typeof(AntiDump).Module;
			var bas = (byte*)Marshal.GetHINSTANCE(module);
			byte* ptr = bas + 0x3c;
			byte* ptr2;
			ptr = ptr2 = bas + *(uint*)ptr;
			ptr += 0x6;
			ushort sectNum = *(ushort*)ptr;
			ptr += 14;
			ushort optSize = *(ushort*)ptr;
			ptr = ptr2 = ptr + 0x4 + optSize;

			byte* @new = stackalloc byte[11];
			if (module.FullyQualifiedName[0] != '<') //Mapped
			{
				//VirtualProtect(ptr - 16, 8, 0x40, out old);
				//*(uint*)(ptr - 12) = 0;
				byte* mdDir = bas + *(uint*)(ptr - 16);
				//*(uint*)(ptr - 16) = 0;

				if (*(uint*)(ptr - 0x78) != 0) {
					byte* importDir = bas + *(uint*)(ptr - 0x78);
					byte* oftMod = bas + *(uint*)importDir;
					byte* modName = bas + *(uint*)(importDir + 12);
					byte* funcName = bas + *(uint*)oftMod + 2;
					VirtualProtect(modName, 11, 0x40, out old);

					*(uint*)@new = 0x6c64746e;
					*((uint*)@new + 1) = 0x6c642e6c;
					*((ushort*)@new + 4) = 0x006c;
					*(@new + 10) = 0;

					for (int i = 0; i < 11; i++)
						*(modName + i) = *(@new + i);

					VirtualProtect(funcName, 11, 0x40, out old);

					*(uint*)@new = 0x6f43744e;
					*((uint*)@new + 1) = 0x6e69746e;
					*((ushort*)@new + 4) = 0x6575;
					*(@new + 10) = 0;

					for (int i = 0; i < 11; i++)
						*(funcName + i) = *(@new + i);
				}

				for (int i = 0; i < sectNum; i++) {
					VirtualProtect(ptr, 8, 0x40, out old);
					Marshal.Copy(new byte[8], 0, (IntPtr)ptr, 8);
					ptr += 0x28;
				}
				VirtualProtect(mdDir, 0x48, 0x40, out old);
				byte* mdHdr = bas + *(uint*)(mdDir + 8);
				*(uint*)mdDir = 0;
				*((uint*)mdDir + 1) = 0;
				*((uint*)mdDir + 2) = 0;
				*((uint*)mdDir + 3) = 0;

				VirtualProtect(mdHdr, 4, 0x40, out old);
				*(uint*)mdHdr = 0;
				mdHdr += 12;
				mdHdr += *(uint*)mdHdr;
				mdHdr = (byte*)(((ulong)mdHdr + 7) & ~3UL);
				mdHdr += 2;
				ushort numOfStream = *mdHdr;
				mdHdr += 2;
				for (int i = 0; i < numOfStream; i++) {
					VirtualProtect(mdHdr, 8, 0x40, out old);
					//*(uint*)mdHdr = 0;
					mdHdr += 4;
					//*(uint*)mdHdr = 0;
					mdHdr += 4;
					for (int ii = 0; ii < 8; ii++) {
						VirtualProtect(mdHdr, 4, 0x40, out old);
						*mdHdr = 0;
						mdHdr++;
						if (*mdHdr == 0) {
							mdHdr += 3;
							break;
						}
						*mdHdr = 0;
						mdHdr++;
						if (*mdHdr == 0) {
							mdHdr += 2;
							break;
						}
						*mdHdr = 0;
						mdHdr++;
						if (*mdHdr == 0) {
							mdHdr += 1;
							break;
						}
						*mdHdr = 0;
						mdHdr++;
					}
				}
			}
			else //Flat
			{
				//VirtualProtect(ptr - 16, 8, 0x40, out old);
				//*(uint*)(ptr - 12) = 0;
				uint mdDir = *(uint*)(ptr - 16);
				//*(uint*)(ptr - 16) = 0;
				uint importDir = *(uint*)(ptr - 0x78);

				var vAdrs = new uint[sectNum];
				var vSizes = new uint[sectNum];
				var rAdrs = new uint[sectNum];
				for (int i = 0; i < sectNum; i++) {
					VirtualProtect(ptr, 8, 0x40, out old);
					Marshal.Copy(new byte[8], 0, (IntPtr)ptr, 8);
					vAdrs[i] = *(uint*)(ptr + 12);
					vSizes[i] = *(uint*)(ptr + 8);
					rAdrs[i] = *(uint*)(ptr + 20);
					ptr += 0x28;
				}


				if (importDir != 0) {
					for (int i = 0; i < sectNum; i++)
						if (vAdrs[i] <= importDir && importDir < vAdrs[i] + vSizes[i]) {
							importDir = importDir - vAdrs[i] + rAdrs[i];
							break;
						}
					byte* importDirPtr = bas + importDir;
					uint oftMod = *(uint*)importDirPtr;
					for (int i = 0; i < sectNum; i++)
						if (vAdrs[i] <= oftMod && oftMod < vAdrs[i] + vSizes[i]) {
							oftMod = oftMod - vAdrs[i] + rAdrs[i];
							break;
						}
					byte* oftModPtr = bas + oftMod;
					uint modName = *(uint*)(importDirPtr + 12);
					for (int i = 0; i < sectNum; i++)
						if (vAdrs[i] <= modName && modName < vAdrs[i] + vSizes[i]) {
							modName = modName - vAdrs[i] + rAdrs[i];
							break;
						}
					uint funcName = *(uint*)oftModPtr + 2;
					for (int i = 0; i < sectNum; i++)
						if (vAdrs[i] <= funcName && funcName < vAdrs[i] + vSizes[i]) {
							funcName = funcName - vAdrs[i] + rAdrs[i];
							break;
						}
					VirtualProtect(bas + modName, 11, 0x40, out old);

					*(uint*)@new = 0x6c64746e;
					*((uint*)@new + 1) = 0x6c642e6c;
					*((ushort*)@new + 4) = 0x006c;
					*(@new + 10) = 0;

					for (int i = 0; i < 11; i++)
						*(bas + modName + i) = *(@new + i);

					VirtualProtect(bas + funcName, 11, 0x40, out old);

					*(uint*)@new = 0x6f43744e;
					*((uint*)@new + 1) = 0x6e69746e;
					*((ushort*)@new + 4) = 0x6575;
					*(@new + 10) = 0;

					for (int i = 0; i < 11; i++)
						*(bas + funcName + i) = *(@new + i);
				}


				for (int i = 0; i < sectNum; i++)
					if (vAdrs[i] <= mdDir && mdDir < vAdrs[i] + vSizes[i]) {
						mdDir = mdDir - vAdrs[i] + rAdrs[i];
						break;
					}
				byte* mdDirPtr = bas + mdDir;
				VirtualProtect(mdDirPtr, 0x48, 0x40, out old);
				uint mdHdr = *(uint*)(mdDirPtr + 8);
				for (int i = 0; i < sectNum; i++)
					if (vAdrs[i] <= mdHdr && mdHdr < vAdrs[i] + vSizes[i]) {
						mdHdr = mdHdr - vAdrs[i] + rAdrs[i];
						break;
					}
				*(uint*)mdDirPtr = 0;
				*((uint*)mdDirPtr + 1) = 0;
				*((uint*)mdDirPtr + 2) = 0;
				*((uint*)mdDirPtr + 3) = 0;


				byte* mdHdrPtr = bas + mdHdr;
				VirtualProtect(mdHdrPtr, 4, 0x40, out old);
				*(uint*)mdHdrPtr = 0;
				mdHdrPtr += 12;
				mdHdrPtr += *(uint*)mdHdrPtr;
				mdHdrPtr = (byte*)(((ulong)mdHdrPtr + 7) & ~3UL);
				mdHdrPtr += 2;
				ushort numOfStream = *mdHdrPtr;
				mdHdrPtr += 2;
				for (int i = 0; i < numOfStream; i++) {
					VirtualProtect(mdHdrPtr, 8, 0x40, out old);
					//*(uint*)mdHdrPtr = 0;
					mdHdrPtr += 4;
					//*(uint*)mdHdrPtr = 0;
					mdHdrPtr += 4;
					for (int ii = 0; ii < 8; ii++) {
						VirtualProtect(mdHdrPtr, 4, 0x40, out old);
						*mdHdrPtr = 0;
						mdHdrPtr++;
						if (*mdHdrPtr == 0) {
							mdHdrPtr += 3;
							break;
						}
						*mdHdrPtr = 0;
						mdHdrPtr++;
						if (*mdHdrPtr == 0) {
							mdHdrPtr += 2;
							break;
						}
						*mdHdrPtr = 0;
						mdHdrPtr++;
						if (*mdHdrPtr == 0) {
							mdHdrPtr += 1;
							break;
						}
						*mdHdrPtr = 0;
						mdHdrPtr++;
					}
				}
			}
		}
	}
}