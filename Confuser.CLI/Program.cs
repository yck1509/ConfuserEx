using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Confuser.Core;
using Confuser.Core.Project;

namespace Confuser.CLI {
	internal class Program {
		private static int Main(string[] args) {
			ConsoleColor original = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.White;
			string originalTitle = Console.Title;
			Console.Title = "ConfuserEx";

			try {
				if (args.Length < 1) {
					PrintUsage();
					return 0;
				}

				var proj = new ConfuserProject();
				try {
					var xmlDoc = new XmlDocument();
					xmlDoc.Load(args[0]);
					proj.Load(xmlDoc);
					proj.BaseDirectory = Path.Combine(Path.GetDirectoryName(args[0]), proj.BaseDirectory);
				} catch (Exception ex) {
					WriteLineWithColor(ConsoleColor.Red, "Failed to load project:");
					WriteLineWithColor(ConsoleColor.Red, ex.ToString());
					return -1;
				}

				var parameters = new ConfuserParameters();
				parameters.Project = proj;
				var logger = new ConsoleLogger();
				parameters.Logger = new ConsoleLogger();

				Console.Title = "ConfuserEx - Running...";
				ConfuserEngine.Run(parameters).Wait();

				if (NeedPause()) {
					Console.WriteLine("Press any key to continue...");
					Console.ReadKey(true);
				}

				return logger.ReturnValue;
			} finally {
				Console.ForegroundColor = original;
				Console.Title = originalTitle;
			}
		}

		private static bool NeedPause() {
			return Debugger.IsAttached || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPT"));
		}

		private static void PrintUsage() {
			WriteLine("Usage:");
			WriteLine("Confuser.CLI.exe <project configuration>");
		}

		private static void WriteLineWithColor(ConsoleColor color, string txt) {
			ConsoleColor original = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(txt);
			Console.ForegroundColor = original;
		}

		private static void WriteLine(string txt) {
			Console.WriteLine(txt);
		}

		private static void WriteLine() {
			Console.WriteLine();
		}

		private class ConsoleLogger : ILogger {
			private readonly DateTime begin;

			public ConsoleLogger() {
				begin = DateTime.Now;
			}

			public int ReturnValue { get; private set; }

			public void Debug(string msg) {
				WriteLineWithColor(ConsoleColor.Gray, "[DEBUG] " + msg);
			}

			public void DebugFormat(string format, params object[] args) {
				WriteLineWithColor(ConsoleColor.Gray, "[DEBUG] " + string.Format(format, args));
			}

			public void Info(string msg) {
				WriteLineWithColor(ConsoleColor.White, " [INFO] " + msg);
			}

			public void InfoFormat(string format, params object[] args) {
				WriteLineWithColor(ConsoleColor.White, " [INFO] " + string.Format(format, args));
			}

			public void Warn(string msg) {
				WriteLineWithColor(ConsoleColor.Yellow, " [WARN] " + msg);
			}

			public void WarnFormat(string format, params object[] args) {
				WriteLineWithColor(ConsoleColor.Yellow, " [WARN] " + string.Format(format, args));
			}

			public void WarnException(string msg, Exception ex) {
				WriteLineWithColor(ConsoleColor.Yellow, " [WARN] " + msg);
				WriteLineWithColor(ConsoleColor.Yellow, "Exception: " + ex);
			}

			public void Error(string msg) {
				WriteLineWithColor(ConsoleColor.Red, "[ERROR] " + msg);
			}

			public void ErrorFormat(string format, params object[] args) {
				WriteLineWithColor(ConsoleColor.Red, "[ERROR] " + string.Format(format, args));
			}

			public void ErrorException(string msg, Exception ex) {
				WriteLineWithColor(ConsoleColor.Red, "[ERROR] " + msg);
				WriteLineWithColor(ConsoleColor.Red, "Exception: " + ex);
			}

			public void Progress(int progress, int overall) { }

			public void EndProgress() { }

			public void Finish(bool successful) {
				DateTime now = DateTime.Now;
				string timeString = string.Format(
					"at {0}, {1}:{2:d2} elapsed.",
					now.ToShortTimeString(),
					(int)now.Subtract(begin).TotalMinutes,
					now.Subtract(begin).Seconds);
				if (successful) {
					Console.Title = "ConfuserEx - Success";
					WriteLineWithColor(ConsoleColor.Green, "Finished " + timeString);
					ReturnValue = 0;
				} else {
					Console.Title = "ConfuserEx - Fail";
					WriteLineWithColor(ConsoleColor.Red, "Failed " + timeString);
					ReturnValue = 1;
				}
			}
		}
	}
}