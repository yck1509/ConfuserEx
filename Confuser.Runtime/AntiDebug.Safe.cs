using System;
using System.Diagnostics;
using System.Threading;

namespace Confuser.Runtime {
	internal static class AntiDebugSafe {
		static void Initialize() {
			string x = "COR";
			var env = typeof(Environment);
			var method = env.GetMethod("GetEnvironmentVariable", new[] { typeof(string) });
			if (method != null &&
			    (method.Invoke(null, new object[] { x + "_PROFILER" }) != null ||
				 method.Invoke(null, new object[] { x + "_ENABLE_PROFILING" }) != null))
				Environment.FailFast(null);

			var thread = new Thread(Worker);
			thread.IsBackground = true;
			thread.Start(null);
		}

		static void Worker(object thread) {
			var th = thread as Thread;
			if (th == null) {
				th = new Thread(Worker);
				th.IsBackground = true;
				th.Start(Thread.CurrentThread);
				Thread.Sleep(500);
			}
			while (true) {
				if (Debugger.IsAttached || Debugger.IsLogging())
					Environment.FailFast(null);

				if (!th.IsAlive)
					Environment.FailFast(null);

				Thread.Sleep(1000);
			}
		}
	}
}