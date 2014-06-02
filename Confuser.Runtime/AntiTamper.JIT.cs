﻿using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Confuser.Runtime {
	internal static unsafe class AntiTamperJIT {
		private static uint* ptr;
		private static uint len;
		private static IntPtr moduleHnd;
		private static compileMethod originalDelegate;

		private static bool ver;

		private static compileMethod handler;

		public static void Initialize() {
			Module m = typeof (AntiTamperNormal).Module;
			string n = m.FullyQualifiedName;
			bool f = n.Length > 0 && n[0] == '<';
			var b = (byte*)Marshal.GetHINSTANCE(m);
			byte* p = b + *(uint*)(b + 0x3c);
			ushort s = *(ushort*)(p + 0x6);
			ushort o = *(ushort*)(p + 0x14);

			uint* e = null;
			uint l = 0;
			var r = (uint*)(p + 0x18 + o);
			uint z = (uint)Mutation.KeyI1, x = (uint)Mutation.KeyI2, c = (uint)Mutation.KeyI3, v = (uint)Mutation.KeyI4;
			for (int i = 0; i < s; i++) {
				uint g = (*r++) * (*r++);
				if (g == (uint)Mutation.KeyI0) {
					e = (uint*)(b + (f ? *(r + 3) : *(r + 1)));
					l = (f ? *(r + 2) : *(r + 0)) >> 2;
				}
				else if (g != 0) {
					var q = (uint*)(b + (f ? *(r + 3) : *(r + 1)));
					uint j = *(r + 2) >> 2;
					for (uint k = 0; k < j; k++) {
						uint t = (z ^ (*q++)) + x + c * v;
						z = x;
						x = c;
						x = v;
						v = t;
					}
				}
				r += 8;
			}

			uint[] y = new uint[0x10], d = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				y[i] = v;
				d[i] = x;
				z = (x >> 5) | (x << 27);
				x = (c >> 3) | (c << 29);
				c = (v >> 7) | (v << 25);
				v = (z >> 11) | (z << 21);
			}
			Mutation.Crypt(y, d);

			uint h = 0;
			uint* u = e;
			for (uint i = 0; i < l; i++) {
				*e ^= y[h & 0xf];
				y[h & 0xf] = (y[h & 0xf] ^ (*e++)) + 0x3dbb2819;
				h++;
			}

			ptr = u + 4;
			len = *ptr++;

			ver = RuntimeEnvironment.GetSystemVersion()[1] == '4';
			ModuleHandle hnd = m.ModuleHandle;
			if (ver) {
				ulong* str = stackalloc ulong[1];
				str[0] = 0x0061746144705f6d; //m_pData.
				moduleHnd = (IntPtr)m.GetType().GetField(new string((sbyte*)str), BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m);
			}
			else
				moduleHnd = *(IntPtr*)(&hnd);

			Hook();
		}

		[DllImport("kernel32.dll")]
		private static extern IntPtr LoadLibrary(string lib);

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetProcAddress(IntPtr lib, string proc);

		[DllImport("kernel32.dll")]
		private static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

		private static void Hook() {
			ulong* ptr = stackalloc ulong[2];
			if (ver) {
				ptr[0] = 0x642e74696a726c63; //clrjit.d
				ptr[1] = 0x0000000000006c6c; //ll......
			}
			else {
				ptr[0] = 0x74696a726f63736d; //mscorjit
				ptr[1] = 0x000000006c6c642e; //.dll....
			}
			IntPtr jit = LoadLibrary(new string((sbyte*)ptr));
			ptr[0] = 0x000074694a746567; //getJit
			var get = (getJit)Marshal.GetDelegateForFunctionPointer(GetProcAddress(jit, new string((sbyte*)ptr)), typeof (getJit));
			IntPtr hookPosition = *get();
			IntPtr original = *(IntPtr*)hookPosition;

			IntPtr trampoline;
			uint oldPl;
			if (IntPtr.Size == 8) {
				trampoline = Marshal.AllocHGlobal(16);
				var tptr = (ulong*)trampoline;
				tptr[0] = 0xffffffffffffb848;
				tptr[1] = 0x90909090e0ffffff;

				VirtualProtect(trampoline, 12, 0x40, out oldPl);
				Marshal.WriteIntPtr(trampoline, 2, original);
			}
			else {
				trampoline = Marshal.AllocHGlobal(8);
				var tptr = (ulong*)trampoline;
				tptr[0] = 0x90e0ffffffffffb8;

				VirtualProtect(trampoline, 7, 0x40, out oldPl);
				Marshal.WriteIntPtr(trampoline, 1, original);
			}

			originalDelegate = (compileMethod)Marshal.GetDelegateForFunctionPointer(trampoline, typeof (compileMethod));
			handler = HookHandler;

			RuntimeHelpers.PrepareDelegate(originalDelegate);
			RuntimeHelpers.PrepareDelegate(handler);

			VirtualProtect(hookPosition, (uint)IntPtr.Size, 0x40, out oldPl);
			Marshal.WriteIntPtr(hookPosition, Marshal.GetFunctionPointerForDelegate(handler));
			VirtualProtect(hookPosition, (uint)IntPtr.Size, oldPl, out oldPl);
		}

		private static void ExtractLocalVars(CORINFO_METHOD_INFO* info, uint len, byte* localVar) {
			void* sigInfo;
			if (ver) {
				if (IntPtr.Size == 8)
					sigInfo = (CORINFO_SIG_INFO_x64*)((uint*)(info + 1) + 5) + 1;
				else
					sigInfo = (CORINFO_SIG_INFO_x86*)((uint*)(info + 1) + 4) + 1;
			}
			else {
				if (IntPtr.Size == 8)
					sigInfo = (CORINFO_SIG_INFO_x64*)((uint*)(info + 1) + 3) + 1;
				else
					sigInfo = (CORINFO_SIG_INFO_x86*)((uint*)(info + 1) + 3) + 1;
			}

			if (IntPtr.Size == 8)
				((CORINFO_SIG_INFO_x64*)sigInfo)->sig = (IntPtr)localVar;
			else
				((CORINFO_SIG_INFO_x86*)sigInfo)->sig = (IntPtr)localVar;
			localVar++;
			byte b = *localVar;
			ushort numArgs;
			IntPtr args;
			if ((b & 0x80) == 0) {
				numArgs = b;
				args = (IntPtr)(localVar + 1);
			}
			else {
				numArgs = (ushort)(((uint)(b & ~0x80) << 8) | *(localVar + 1));
				args = (IntPtr)(localVar + 2);
			}

			if (IntPtr.Size == 8) {
				var sigInfox64 = (CORINFO_SIG_INFO_x64*)sigInfo;
				sigInfox64->callConv = 0;
				sigInfox64->retType = 1;
				sigInfox64->flags = 1;
				sigInfox64->numArgs = numArgs;
				sigInfox64->args = args;
			}
			else {
				var sigInfox86 = (CORINFO_SIG_INFO_x86*)sigInfo;
				sigInfox86->callConv = 0;
				sigInfox86->retType = 1;
				sigInfox86->flags = 1;
				sigInfox86->numArgs = numArgs;
				sigInfox86->args = args;
			}
		}

		private static uint HookHandler(IntPtr self, ICorJitInfo* comp, CORINFO_METHOD_INFO* info, uint flags, byte** nativeEntry, uint* nativeSizeOfCode) {
			if (info != null &&
			    info->scope == moduleHnd &&
			    info->ILCode[0] == 0x14) {
				ICorClassInfo* clsInfo = ICorStaticInfo.ICorClassInfo(ICorDynamicInfo.ICorStaticInfo(ICorJitInfo.ICorDynamicInfo(comp)));
				int gmdSlot = 12 + (ver ? 2 : IntPtr.Size / 4);
				var getMethodDef = (getMethodDefFromMethod)Marshal.GetDelegateForFunctionPointer(clsInfo->vfptr[gmdSlot], typeof (getMethodDefFromMethod));
				uint token = ((uint)(0x06000000 + *(ushort*)info->ftn));

				uint lo = 0, hi = len;
				uint? offset = null;
				while (hi >= lo) {
					uint mid = lo + ((hi - lo) >> 1);
					uint midTok = *(ptr + (mid << 1));
					if (midTok == token) {
						offset = *(ptr + (mid << 1) + 1);
						break;
					}
					if (midTok < token)
						lo = mid + 1;
					else
						hi = mid - 1;
				}
				if (offset == null)
					return originalDelegate(self, comp, info, flags, nativeEntry, nativeSizeOfCode);

				uint* dataPtr = ptr + (uint)offset;
				uint dataLen = *dataPtr++;
				var newPtr = (uint*)Marshal.AllocHGlobal((int)dataLen << 2);
				try {
					var data = (MethodData*)newPtr;
					uint* copyData = newPtr;

					uint state = token * (uint)Mutation.KeyI0;
					uint counter = state;
					for (uint i = 0; i < dataLen; i++) {
						*copyData = *dataPtr++ ^ state;
						state += (*copyData++) ^ counter;
						counter ^= (state >> 5) | (state << 27);
					}

					info->ILCodeSize = data->ILCodeSize;
					if (ver) {
						*((uint*)(info + 1) + 0) = data->MaxStack;
						*((uint*)(info + 1) + 1) = data->EHCount;
						*((uint*)(info + 1) + 2) = data->Options;
					}
					else {
						*((ushort*)(info + 1) + 0) = (ushort)data->MaxStack;
						*((ushort*)(info + 1) + 1) = (ushort)data->EHCount;
						*((uint*)(info + 1) + 1) = data->Options;
					}

					var body = (byte*)(data + 1);

					info->ILCode = body;
					body += info->ILCodeSize;

					if (data->LocalVars != 0) {
						ExtractLocalVars(info, data->LocalVars, body);
						body += data->LocalVars;
					}

					var ehPtr = (CORINFO_EH_CLAUSE*)body;

					CorMethodInfoHook hook1 = CorMethodInfoHook.Hook(comp, info->ftn, ehPtr);
					uint ret = originalDelegate(self, comp, info, flags, nativeEntry, nativeSizeOfCode);
					hook1.Dispose();

					return ret;
				}
				finally {
					Marshal.FreeHGlobal((IntPtr)newPtr);
				}
			}
			return originalDelegate(self, comp, info, flags, nativeEntry, nativeSizeOfCode);
		}

		#region JIT internal

		private static bool hasLinkInfo;

		[StructLayout(LayoutKind.Sequential, Size = 0x18)]
		private struct CORINFO_EH_CLAUSE { }

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct CORINFO_METHOD_INFO {
			public readonly IntPtr ftn;
			public readonly IntPtr scope;
			public byte* ILCode;
			public uint ILCodeSize;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct CORINFO_SIG_INFO_x64 {
			public uint callConv;
			private readonly uint pad1;
			public readonly IntPtr retTypeClass;
			public readonly IntPtr retTypeSigClass;
			public byte retType;
			public byte flags;
			public ushort numArgs;
			private readonly uint pad2;
			public readonly CORINFO_SIG_INST_x64 sigInst;
			public IntPtr args;
			public IntPtr sig;
			public readonly IntPtr scope;
			public readonly uint token;
			private readonly uint pad3;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct CORINFO_SIG_INFO_x86 {
			public uint callConv;
			public readonly IntPtr retTypeClass;
			public readonly IntPtr retTypeSigClass;
			public byte retType;
			public byte flags;
			public ushort numArgs;
			public readonly CORINFO_SIG_INST_x86 sigInst;
			public IntPtr args;
			public IntPtr sig;
			public readonly IntPtr scope;
			public readonly uint token;
		}

		[StructLayout(LayoutKind.Sequential, Size = 32)]
		private struct CORINFO_SIG_INST_x64 { }

		[StructLayout(LayoutKind.Sequential, Size = 16)]
		private struct CORINFO_SIG_INST_x86 { }

		[StructLayout(LayoutKind.Sequential)]
		private struct ICorClassInfo {
			public IntPtr* vfptr;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct ICorDynamicInfo {
			public IntPtr* vfptr;
			public int* vbptr;

			public static ICorStaticInfo* ICorStaticInfo(ICorDynamicInfo* ptr) {
				return (ICorStaticInfo*)((byte*)&ptr->vbptr + ptr->vbptr[hasLinkInfo ? 9 : 8]);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct ICorJitInfo {
			public IntPtr* vfptr;
			public int* vbptr;

			public static ICorDynamicInfo* ICorDynamicInfo(ICorJitInfo* ptr) {
				hasLinkInfo = ptr->vbptr[10] > 0 && ptr->vbptr[10] >> 16 == 0; // != 0 and hiword byte == 0
				return (ICorDynamicInfo*)((byte*)&ptr->vbptr + ptr->vbptr[hasLinkInfo ? 10 : 9]);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct ICorMethodInfo {
			public IntPtr* vfptr;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct ICorModuleInfo {
			public IntPtr* vfptr;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct ICorStaticInfo {
			public IntPtr* vfptr;
			public int* vbptr;

			public static ICorMethodInfo* ICorMethodInfo(ICorStaticInfo* ptr) {
				return (ICorMethodInfo*)((byte*)&ptr->vbptr + ptr->vbptr[1]);
			}

			public static ICorModuleInfo* ICorModuleInfo(ICorStaticInfo* ptr) {
				return (ICorModuleInfo*)((byte*)&ptr->vbptr + ptr->vbptr[2]);
			}

			public static ICorClassInfo* ICorClassInfo(ICorStaticInfo* ptr) {
				return (ICorClassInfo*)((byte*)&ptr->vbptr + ptr->vbptr[3]);
			}
		}

		#endregion

		private class CorMethodInfoHook {
			private static int ehNum = -1;
			public CORINFO_EH_CLAUSE* clauses;
			public IntPtr ftn;
			public ICorMethodInfo* info;
			public getEHinfo n_getEHinfo;
			public IntPtr* newVfTbl;

			public getEHinfo o_getEHinfo;
			public IntPtr* oldVfTbl;

			private void hookEHInfo(IntPtr self, IntPtr ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause) {
				if (ftn == this.ftn) {
					*clause = clauses[EHnumber];
				}
				else {
					o_getEHinfo(self, ftn, EHnumber, clause);
				}
			}

			public void Dispose() {
				Marshal.FreeHGlobal((IntPtr)newVfTbl);
				info->vfptr = oldVfTbl;
			}

			public static CorMethodInfoHook Hook(ICorJitInfo* comp, IntPtr ftn, CORINFO_EH_CLAUSE* clauses) {
				ICorMethodInfo* mtdInfo = ICorStaticInfo.ICorMethodInfo(ICorDynamicInfo.ICorStaticInfo(ICorJitInfo.ICorDynamicInfo(comp)));
				IntPtr* vfTbl = mtdInfo->vfptr;
				const int SLOT_NUM = 0x1B;
				var newVfTbl = (IntPtr*)Marshal.AllocHGlobal(SLOT_NUM * IntPtr.Size);
				for (int i = 0; i < SLOT_NUM; i++)
					newVfTbl[i] = vfTbl[i];
				if (ehNum == -1)
					for (int i = 0; i < SLOT_NUM; i++) {
						bool isEh = true;
						for (var func = (byte*)vfTbl[i]; *func != 0xe9; func++)
							if (IntPtr.Size == 8 ?
								    (*func == 0x48 && *(func + 1) == 0x81 && *(func + 2) == 0xe9) :
								    (*func == 0x83 && *(func + 1) == 0xe9)) {
								isEh = false;
								break;
							}
						if (isEh) {
							ehNum = i;
							break;
						}
					}

				var ret = new CorMethodInfoHook {
					ftn = ftn,
					info = mtdInfo,
					clauses = clauses,
					newVfTbl = newVfTbl,
					oldVfTbl = vfTbl
				};

				ret.n_getEHinfo = ret.hookEHInfo;
				ret.o_getEHinfo = (getEHinfo)Marshal.GetDelegateForFunctionPointer(vfTbl[ehNum], typeof (getEHinfo));
				newVfTbl[ehNum] = Marshal.GetFunctionPointerForDelegate(ret.n_getEHinfo);

				mtdInfo->vfptr = newVfTbl;
				return ret;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MethodData {
			public readonly uint ILCodeSize;
			public readonly uint MaxStack;
			public readonly uint EHCount;
			public readonly uint LocalVars;
			public readonly uint Options;
			public readonly uint MulSeed;
		}

		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		private delegate uint compileMethod(IntPtr self, ICorJitInfo* comp, CORINFO_METHOD_INFO* info, uint flags, byte** nativeEntry, uint* nativeSizeOfCode);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void getEHinfo(IntPtr self, IntPtr ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause);

		private delegate IntPtr* getJit();

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate uint getMethodDefFromMethod(IntPtr self, IntPtr ftn);
	}
}