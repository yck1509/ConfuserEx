using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Confuser.Runtime {
	internal static class AntiDebugWin32 {
		static void Initialize() {
			string x = "COR";
			if (Environment.GetEnvironmentVariable(x + "_PROFILER") != null ||
			    Environment.GetEnvironmentVariable(x + "_ENABLE_PROFILING") != null)
				Environment.FailFast(null);

			var thread = new Thread(Worker);
			thread.IsBackground = true;
			thread.Start(null);
		}

		[DllImport("kernel32.dll")]
		static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll")]
		static extern bool IsDebuggerPresent();

		[DllImport("kernel32.dll")]
		static extern int OutputDebugString(string str);

		static void Worker(object thread) {
			var th = thread as Thread;
			if (th == null) {
				th = new Thread(Worker);
				th.IsBackground = true;
				th.Start(Thread.CurrentThread);
				Thread.Sleep(500);
			}
			while (true) {
				// Managed
				if (Debugger.IsAttached || Debugger.IsLogging())
					Environment.FailFast("");

				// IsDebuggerPresent
				if (IsDebuggerPresent())
					Environment.FailFast("");

				// OpenProcess
				Process ps = Process.GetCurrentProcess();
				if (ps.Handle == IntPtr.Zero)
					Environment.FailFast("");
				ps.Close();

				// OutputDebugString
				if (OutputDebugString("") > IntPtr.Size)
					Environment.FailFast("");

				// CloseHandle
				try {
					CloseHandle(IntPtr.Zero);
				}
				catch {
					Environment.FailFast("");
				}

				if (!th.IsAlive)
					Environment.FailFast("");

				Thread.Sleep(1000);
			}
		}
	}
}