using System;

namespace Confuser.Runtime {
	static partial class AntiDebugAntinet {
		static void Initialize() {
			if (!InitializeAntiDebugger())
				Environment.FailFast(null);
			InitializeAntiProfiler();
			if (IsProfilerAttached) {
				Environment.FailFast(null);
				PreventActiveProfilerFromReceivingProfilingMessages();
			}
		}
	}
}