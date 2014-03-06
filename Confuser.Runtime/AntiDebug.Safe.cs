using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace Confuser.Runtime
{
    static class AntiDebugSafe
    {
        static unsafe void Initialize()
        {
            string x = "COR";
            if (Environment.GetEnvironmentVariable(x + "_PROFILER") != null ||
                Environment.GetEnvironmentVariable(x + "_ENABLE_PROFILING") != null)
                Environment.FailFast(null);

            Thread thread = new Thread(Worker);
            thread.IsBackground = true;
            thread.Start(null);
        }

        static void Worker(object thread)
        {
            Thread th = thread as Thread;
            if (th == null)
            {
                th = new Thread(Worker);
                th.IsBackground = true;
                th.Start(Thread.CurrentThread);
                Thread.Sleep(500);
            }
            while (true)
            {
                if (Debugger.IsAttached || Debugger.IsLogging())
                    Environment.FailFast(null);

                if (!th.IsAlive)
                    Environment.FailFast(null);

                Thread.Sleep(1000);
            }
        }
    }
}
