/*
 * Anti managed debugger code. Written by de4dot@gmail.com
 * This code is in the public domain.
 * Official site: https://github.com/0xd4d/antinet
 */

using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;

namespace Confuser.Runtime {
	// This class will make sure that no managed .NET debugger can attach and
	// debug this .NET process. This code assumes that it's Microsoft's .NET
	// implementation (for the desktop) that is used. The only currently supported
	// versions are .NET Framework 2.0 - 4.5 (CLR 2.0 and CLR 4.0).
	// It prevents debugging by killing the .NET debugger thread. When it's killed,
	// any attached managed debugger, or any managed debugger that attaches, will
	// fail to send and receive any .NET debug messages. If a debugger is attached,
	// <c>Debugger.IsAttached</c> will still return <c>true</c> but that doesn't mean
	// the debugger is working. It's just that the debugger flag isn't reset by our code.
	// If a debugger is attached before this code is executed, the process could hang at
	// some later time when this process is trying to send a debug message to the debugger.
	// Clearing the debug flag could possibly solve this if you don't want it to hang.
	static partial class AntiDebugAntinet {

		[DllImport("kernel32", CharSet = CharSet.Auto)]
		private static extern uint GetCurrentProcessId();

		[DllImport("kernel32")]
		private static extern bool SetEvent(IntPtr hEvent);

		/// <summary>
		///     Must be called to initialize anti-managed debugger code
		/// </summary>
		/// <returns><c>true</c> if successful, <c>false</c> otherwise</returns>
		private static unsafe bool InitializeAntiDebugger() {
			Info info = GetInfo();
			IntPtr pDebuggerRCThread = FindDebuggerRCThreadAddress(info);
			if (pDebuggerRCThread == IntPtr.Zero)
				return false;

			// This isn't needed but it will at least stop debuggers from attaching.
			// Even if they did attach, they wouldn't get any messages since the debugger
			// thread has exited. A user who tries to attach will be greeted with an
			// "unable to attach due to different versions etc" message. This will not stop
			// already attached debuggers. Killing the debugger thread will.
			var pDebuggerIPCControlBlock = (byte*)*(IntPtr*)((byte*)pDebuggerRCThread + info.DebuggerRCThread_pDebuggerIPCControlBlock);
			if (Environment.Version.Major == 2)
				pDebuggerIPCControlBlock = (byte*)*(IntPtr*)pDebuggerIPCControlBlock;
			// Set size field to 0. mscordbi!CordbProcess::VerifyControlBlock() will fail
			// when it detects an unknown size.
			*(uint*)pDebuggerIPCControlBlock = 0;

			// Signal debugger thread to exit
			*((byte*)pDebuggerRCThread + info.DebuggerRCThread_shouldKeepLooping) = 0;
			IntPtr hEvent = *(IntPtr*)((byte*)pDebuggerRCThread + info.DebuggerRCThread_hEvent1);
			SetEvent(hEvent);

			return true;
		}

		/// <summary>
		///     Returns the correct <see cref="Info" /> instance
		/// </summary>
		private static Info GetInfo() {
			switch (Environment.Version.Major) {
				case 2:
					return IntPtr.Size == 4 ? Infos.info_CLR20_x86 : Infos.info_CLR20_x64;
				case 4:
					if (Environment.Version.Revision <= 17020)
						return IntPtr.Size == 4 ? Infos.info_CLR40_x86_1 : Infos.info_CLR40_x64;
					return IntPtr.Size == 4 ? Infos.info_CLR40_x86_2 : Infos.info_CLR40_x64;
				default:
					goto case 4; // Assume CLR 4.0
			}
		}

		/// <summary>
		///     Tries to find the address of the <c>DebuggerRCThread</c> instance in memory
		/// </summary>
		/// <param name="info">The debugger info we need</param>
		[HandleProcessCorruptedStateExceptions, SecurityCritical] // Req'd on .NET 4.0
		private static unsafe IntPtr FindDebuggerRCThreadAddress(Info info) {
			uint pid = GetCurrentProcessId();

			try {
				PEInfo peInfo = PEInfo.GetCLR();
				if (peInfo == null)
					return IntPtr.Zero;

				IntPtr sectionAddr;
				uint sectionSize;
				if (!peInfo.FindSection(".data", out sectionAddr, out sectionSize))
					return IntPtr.Zero;

				// Try to find the Debugger instance location in the data section
				var p = (byte*)sectionAddr;
				byte* end = (byte*)sectionAddr + sectionSize;
				for (; p + IntPtr.Size <= end; p += IntPtr.Size) {
					IntPtr pDebugger = *(IntPtr*)p;
					if (pDebugger == IntPtr.Zero)
						continue;

					try {
						// All allocations are pointer-size aligned
						if (!PEInfo.IsAlignedPointer(pDebugger))
							continue;

						// Make sure pid is correct
						uint pid2 = *(uint*)((byte*)pDebugger + info.Debugger_pid);
						if (pid != pid2)
							continue;

						IntPtr pDebuggerRCThread = *(IntPtr*)((byte*)pDebugger + info.Debugger_pDebuggerRCThread);

						// All allocations are pointer-size aligned
						if (!PEInfo.IsAlignedPointer(pDebuggerRCThread))
							continue;

						// Make sure it points back to Debugger
						IntPtr pDebugger2 = *(IntPtr*)((byte*)pDebuggerRCThread + info.DebuggerRCThread_pDebugger);
						if (pDebugger != pDebugger2)
							continue;

						return pDebuggerRCThread;
					}
					catch { }
				}
			}
			catch { }

			return IntPtr.Zero;
		}

		private class Info {

			/// <summary>
			///     Offset in <c>DebuggerRCThread</c> of event to signal to wake it up.
			///     See <c>Debugger::StopDebugger()</c> or one of the first methods it calls.
			/// </summary>
			public int DebuggerRCThread_hEvent1;

			/// <summary>
			///     Offset in <c>DebuggerRCThread</c> of pointer to <c>Debugger</c>.
			///     See <c>DebuggerRCThread::DebuggerRCThread()</c>.
			/// </summary>
			public int DebuggerRCThread_pDebugger;

			/// <summary>
			///     Offset in <c>DebuggerRCThread</c> of pointer to <c>DebuggerIPCControlBlock</c>.
			///     See <c>DebuggerRCThread::Start() after it creates the thread.</c>.
			/// </summary>
			public int DebuggerRCThread_pDebuggerIPCControlBlock;

			/// <summary>
			///     Offset in <c>DebuggerRCThread</c> of keep-looping boolean (1 byte).
			///     See <c>Debugger::StopDebugger()</c> or one of the first methods it calls.
			/// </summary>
			public int DebuggerRCThread_shouldKeepLooping;

			/// <summary>
			///     Offset in <c>Debugger</c> of pointer to <c>DebuggerRCThread</c>.
			///     See <c>Debugger::Startup()</c> (after creating DebuggerRCThread).
			/// </summary>
			public int Debugger_pDebuggerRCThread;

			/// <summary>
			///     Offset in <c>Debugger</c> of the <c>pid</c>.
			///     See <c>Debugger::Debugger()</c>.
			/// </summary>
			public int Debugger_pid;

		}

		private static class Infos {

			/// <summary>
			///     CLR 2.0 x86 offsets
			/// </summary>
			public static readonly Info info_CLR20_x86 = new Info {
				Debugger_pDebuggerRCThread = 4,
				Debugger_pid = 8,
				DebuggerRCThread_pDebugger = 0x30,
				DebuggerRCThread_pDebuggerIPCControlBlock = 0x34,
				DebuggerRCThread_shouldKeepLooping = 0x3C,
				DebuggerRCThread_hEvent1 = 0x40,
			};

			/// <summary>
			///     CLR 2.0 x64 offsets
			/// </summary>
			public static readonly Info info_CLR20_x64 = new Info {
				Debugger_pDebuggerRCThread = 8,
				Debugger_pid = 0x10,
				DebuggerRCThread_pDebugger = 0x58,
				DebuggerRCThread_pDebuggerIPCControlBlock = 0x60,
				DebuggerRCThread_shouldKeepLooping = 0x70,
				DebuggerRCThread_hEvent1 = 0x78,
			};

			/// <summary>
			///     CLR 4.0 x86 offsets
			/// </summary>
			public static readonly Info info_CLR40_x86_1 = new Info {
				Debugger_pDebuggerRCThread = 8,
				Debugger_pid = 0xC,
				DebuggerRCThread_pDebugger = 0x34,
				DebuggerRCThread_pDebuggerIPCControlBlock = 0x38,
				DebuggerRCThread_shouldKeepLooping = 0x40,
				DebuggerRCThread_hEvent1 = 0x44,
			};

			/// <summary>
			///     CLR 4.0 x86 offsets (rev >= 17379 (.NET 4.5 Beta, but not .NET 4.5 Dev Preview))
			/// </summary>
			public static readonly Info info_CLR40_x86_2 = new Info {
				Debugger_pDebuggerRCThread = 8,
				Debugger_pid = 0xC,
				DebuggerRCThread_pDebugger = 0x30,
				DebuggerRCThread_pDebuggerIPCControlBlock = 0x34,
				DebuggerRCThread_shouldKeepLooping = 0x3C,
				DebuggerRCThread_hEvent1 = 0x40,
			};

			/// <summary>
			///     CLR 4.0 x64 offsets (this is the same in all CLR 4.0 versions, even in .NET 4.5 RTM)
			/// </summary>
			public static readonly Info info_CLR40_x64 = new Info {
				Debugger_pDebuggerRCThread = 0x10,
				Debugger_pid = 0x18,
				DebuggerRCThread_pDebugger = 0x58,
				DebuggerRCThread_pDebuggerIPCControlBlock = 0x60,
				DebuggerRCThread_shouldKeepLooping = 0x70,
				DebuggerRCThread_hEvent1 = 0x78,
			};

		}

	}
}