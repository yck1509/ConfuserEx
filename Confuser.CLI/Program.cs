using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Project;
using Confuser.Core;
using System.Xml;
using System.Diagnostics;

namespace Confuser.CLI
{
    class Program
    {
        static int Main(string[] args)
        {
            var original = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;

            try
            {
                if (args.Length != 1)
                {
                    PrintUsage();
                    return 0;
                }

                ConfuserProject proj = new ConfuserProject();
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(args[0]);
                    proj.Load(xmlDoc);
                }
                catch (Exception ex)
                {
                    WriteLineWithColor(ConsoleColor.Red, "Failed to load project:");
                    WriteLineWithColor(ConsoleColor.Red, ex.ToString());
                    return -1;
                }

                var parameters = new ConfuserParameters();
                parameters.Project = proj;
                var logger = new ConsoleLogger();
                parameters.Logger = new ConsoleLogger();

                ConfuserEngine.Run(parameters).Wait();

                if (NeedPause())
                {
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                }

                return logger.ReturnValue;
            }
            finally
            {
                Console.ForegroundColor = original;
            }
        }

        static bool NeedPause()
        {
            return Debugger.IsAttached || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPT"));
        }

        static void PrintUsage()
        {
            WriteLine("Usage:");
            WriteLine("Confuser.CLI.exe <project configuration>");
        }

        static void WriteLineWithColor(ConsoleColor color, string txt)
        {
            ConsoleColor original = System.Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(txt);
            Console.ForegroundColor = original;
        }

        static void WriteLine(string txt)
        {
            Console.WriteLine(txt);
        }

        static void WriteLine()
        {
            Console.WriteLine();
        }

        class ConsoleLogger : ILogger
        {
            public int ReturnValue { get; private set; }

            public void Debug(string msg)
            {
                WriteLineWithColor(ConsoleColor.Gray, "[DEBUG] " + msg);
            }

            public void DebugFormat(string format, params object[] args)
            {
                WriteLineWithColor(ConsoleColor.Gray, "[DEBUG] " + string.Format(format, args));
            }

            public void Info(string msg)
            {
                WriteLineWithColor(ConsoleColor.White, " [INFO] " + msg);
            }

            public void InfoFormat(string format, params object[] args)
            {
                WriteLineWithColor(ConsoleColor.White, " [INFO] " + string.Format(format, args));
            }

            public void Warn(string msg)
            {
                WriteLineWithColor(ConsoleColor.Yellow, " [WARN] " + msg);
            }

            public void WarnFormat(string format, params object[] args)
            {
                WriteLineWithColor(ConsoleColor.Yellow, " [WARN] " + string.Format(format, args));
            }

            public void WarnException(string msg, Exception ex)
            {
                WriteLineWithColor(ConsoleColor.Yellow, " [WARN] " + msg);
                WriteLineWithColor(ConsoleColor.Yellow, "Exception: " + ex.ToString());
            }

            public void Error(string msg)
            {
                WriteLineWithColor(ConsoleColor.Yellow, "[ERROR] " + msg);
            }

            public void ErrorFormat(string format, params object[] args)
            {
                WriteLineWithColor(ConsoleColor.Yellow, "[ERROR] " + string.Format(format, args));
            }

            public void ErrorException(string msg, Exception ex)
            {
                WriteLineWithColor(ConsoleColor.Yellow, "[ERROR] " + msg);
                WriteLineWithColor(ConsoleColor.Yellow, "Exception: " + ex.ToString());
            }

            public void Progress(int overall, int progress)
            {
                WriteLineWithColor(ConsoleColor.Gray, string.Format("{0}/{1}", progress, overall));
            }

            public void Finish(bool successful)
            {
                if (successful)
                    WriteLineWithColor(ConsoleColor.Green, "Finished at " + DateTime.Now.ToShortTimeString());
                else
                    WriteLineWithColor(ConsoleColor.Green, "Failed at " + DateTime.Now.ToShortTimeString());
            }
        }
    }
}
