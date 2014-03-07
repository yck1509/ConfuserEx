using System;

namespace Confuser.Runtime
{
    static partial class AntiDebugAntinet
    {
        static unsafe void Initialize()
        {
            if (!InitializeAntiDebugger())
                Environment.FailFast(null);
            InitializeAntiProfiler();
            if (IsProfilerAttached)
            {
                Environment.FailFast(null);
                PreventActiveProfilerFromReceivingProfilingMessages();
            }
        }
    }
}
