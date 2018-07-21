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
            //Anti dnspy
            Process here = GetParentProcess();
            if (here.ProcessName.ToLower().Contains("dnspy"))
                Environment.FailFast("");

            var thread = new Thread(Worker);
			thread.IsBackground = true;
			thread.Start(null);
		}

        //https://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way

        private static ParentProcessUtilities PPU;
        public static Process GetParentProcess()
        {
            return PPU.GetParentProcess();
        }

        /// <summary>
        /// A utility class to determine a process parent.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        struct ParentProcessUtilities
        {
            // These members must match PROCESS_BASIC_INFORMATION
            internal IntPtr Reserved1;
            internal IntPtr PebBaseAddress;
            internal IntPtr Reserved2_0;
            internal IntPtr Reserved2_1;
            internal IntPtr UniqueProcessId;
            internal IntPtr InheritedFromUniqueProcessId;

            [DllImport("ntdll.dll")]
            private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtilities processInformation, int processInformationLength, out int returnLength);



            /// <summary>
            /// Gets the parent process of the current process.
            /// </summary>
            /// <returns>An instance of the Process class.</returns>
            internal Process GetParentProcess()
            {
                return GetParentProcess(Process.GetCurrentProcess().Handle);
            }

            /// <summary>
            /// Gets the parent process of specified process.
            /// </summary>
            /// <param name="id">The process id.</param>
            /// <returns>An instance of the Process class.</returns>
            public static Process GetParentProcess(int id)
            {
                Process process = Process.GetProcessById(id);
                return GetParentProcess(process.Handle);
            }

            /// <summary>
            /// Gets the parent process of a specified process.
            /// </summary>
            /// <param name="handle">The process handle.</param>
            /// <returns>An instance of the Process class.</returns>
            public static Process GetParentProcess(IntPtr handle)
            {
                ParentProcessUtilities pbi = new ParentProcessUtilities();
                int returnLength;
                int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
                if (status != 0)
                    throw new System.ComponentModel.Win32Exception(status);

                try
                {
                    return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
                }
                catch (ArgumentException)
                {
                    // not found
                    return null;
                }
            }
        }

        [DllImport("kernel32.dll")]
		static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll")]
		static extern bool IsDebuggerPresent();

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
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
