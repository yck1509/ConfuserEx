/*
 * PE helper code. Written by de4dot@gmail.com
 * This code is in the public domain.
 * Official site: https://github.com/0xd4d/antinet
 */

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Confuser.Runtime {
	static partial class AntiDebugAntinet {

		private class PEInfo {

			private readonly IntPtr imageBase;
			private IntPtr imageEnd;
			private int numSects;
			private IntPtr sectionsAddr;

			/// <summary>
			///     Constructor
			/// </summary>
			/// <param name="addr">Address of a PE image</param>
			public PEInfo(IntPtr addr) {
				imageBase = addr;
				Init();
			}

			[DllImport("kernel32", CharSet = CharSet.Auto)]
			private static extern IntPtr GetModuleHandle(string name);

			/// <summary>
			///     Creates a <see cref="PEInfo" /> instance loaded from the CLR (clr.dll / mscorwks.dll)
			/// </summary>
			/// <returns>The new instance or <c>null</c> if we failed</returns>
			public static PEInfo GetCLR() {
				IntPtr clrAddr = GetCLRAddress();
				if (clrAddr == IntPtr.Zero)
					return null;
				return new PEInfo(clrAddr);
			}

			private static IntPtr GetCLRAddress() {
				if (Environment.Version.Major == 2)
					return GetModuleHandle("mscorwks");
				return GetModuleHandle("clr");
			}

			private unsafe void Init() {
				var p = (byte*)imageBase;
				p += *(uint*)(p + 0x3C); // Get NT headers
				p += 4 + 2; // Skip magic + machine
				numSects = *(ushort*)p;
				p += 2 + 0x10; // Skip the rest of file header
				bool is32 = *(ushort*)p == 0x010B;
				uint sizeOfImage = *(uint*)(p + 0x38);
				imageEnd = new IntPtr((byte*)imageBase + sizeOfImage);
				p += is32 ? 0x60 : 0x70; // Skip optional header
				p += 0x10 * 8; // Skip data dirs
				sectionsAddr = new IntPtr(p);
			}

			/// <summary>
			///     Checks whether the address is within the image
			/// </summary>
			/// <param name="addr">Address</param>
			public unsafe bool IsValidImageAddress(IntPtr addr) {
				return IsValidImageAddress((void*)addr, 0);
			}

			/// <summary>
			///     Checks whether the address is within the image
			/// </summary>
			/// <param name="addr">Address</param>
			/// <param name="size">Number of bytes</param>
			public unsafe bool IsValidImageAddress(IntPtr addr, uint size) {
				return IsValidImageAddress((void*)addr, size);
			}

			/// <summary>
			///     Checks whether the address is within the image
			/// </summary>
			/// <param name="addr">Address</param>
			public unsafe bool IsValidImageAddress(void* addr) {
				return IsValidImageAddress(addr, 0);
			}

			/// <summary>
			///     Checks whether the address is within the image
			/// </summary>
			/// <param name="addr">Address</param>
			/// <param name="size">Number of bytes</param>
			public unsafe bool IsValidImageAddress(void* addr, uint size) {
				if (addr < (void*)imageBase)
					return false;
				if (addr >= (void*)imageEnd)
					return false;

				if (size != 0) {
					if ((byte*)addr + size < addr)
						return false;
					if ((byte*)addr + size > (void*)imageEnd)
						return false;
				}

				return true;
			}

			/// <summary>
			///     Finds a section
			/// </summary>
			/// <param name="name">Name of section</param>
			/// <param name="sectionStart">Updated with start address of section</param>
			/// <param name="sectionSize">Updated with size of section</param>
			/// <returns><c>true</c> on success, <c>false</c> on failure</returns>
			public unsafe bool FindSection(string name, out IntPtr sectionStart, out uint sectionSize) {
				byte[] nameBytes = Encoding.UTF8.GetBytes(name + "\0\0\0\0\0\0\0\0");
				for (int i = 0; i < numSects; i++) {
					byte* p = (byte*)sectionsAddr + i * 0x28;
					if (!CompareSectionName(p, nameBytes))
						continue;

					sectionStart = new IntPtr((byte*)imageBase + *(uint*)(p + 12));
					sectionSize = Math.Max(*(uint*)(p + 8), *(uint*)(p + 16));
					return true;
				}

				sectionStart = IntPtr.Zero;
				sectionSize = 0;
				return false;
			}

			private static unsafe bool CompareSectionName(byte* sectionName, byte[] nameBytes) {
				for (int i = 0; i < 8; i++) {
					if (*sectionName != nameBytes[i])
						return false;
					sectionName++;
				}
				return true;
			}

			/// <summary>
			///     Checks whether a pointer is aligned
			/// </summary>
			/// <param name="addr">Address</param>
			public static bool IsAlignedPointer(IntPtr addr) {
				return ((int)addr.ToInt64() & (IntPtr.Size - 1)) == 0;
			}

			/// <summary>
			///     Checks whether a pointer is aligned
			/// </summary>
			/// <param name="addr">Address</param>
			/// <param name="alignment">Alignment</param>
			public static bool IsAligned(IntPtr addr, uint alignment) {
				return ((uint)addr.ToInt64() & (alignment - 1)) == 0;
			}

		}

	}
}