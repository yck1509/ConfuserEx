using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
