using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Confuser.Core;
using Confuser.Core.Project;
using NDesk.Options;

namespace Confuser.CLI {
	internal class Program {
		static int Main(string[] args) {
			ConsoleColor original = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.White;
			string originalTitle = Console.Title;
			Console.Title = "ConfuserEx";

			bool noPause = false;
			string outDir = null;
			var p = new OptionSet {
				{
					"n|nopause", "no pause after finishing protection.",
					value => { noPause = (value != null); }
				}, {
					"o|out=", "specifies output directory.",
					value => { outDir = value; }
				}
			};

			List<string> files;
			try {
				files = p.Parse(args);
				if (files.Count == 0)
					throw new ArgumentException("No input files specified.");
			}
			catch (Exception ex) {
				Console.Write("ConfuserEx.CLI: ");
				Console.WriteLine(ex.Message);
				PrintUsage();
				return -1;
			}

			try {
				var parameters = new ConfuserParameters();

				if (files.Count == 1 && Path.GetExtension(files[0]) == ".crproj") {
					var proj = new ConfuserProject();
					try {
						var xmlDoc = new XmlDocument();
						xmlDoc.Load(files[0]);
						proj.Load(xmlDoc);
						proj.BaseDirectory = Path.Combine(Path.GetDirectoryName(files[0]), proj.BaseDirectory);
					}
					catch (Exception ex) {
						WriteLineWithColor(ConsoleColor.Red, "Failed to load project:");
						WriteLineWithColor(ConsoleColor.Red, ex.ToString());
						return -1;
					}

					parameters.Project = proj;
				}
				else {
					// Generate a ConfuserProject for input modules
					// Assuming first file = main module
					var proj = new ConfuserProject();
					foreach (var input in files)
						proj.Add(new ProjectModule { Path = input });
					proj.BaseDirectory = Path.GetDirectoryName(files[0]);
					proj.OutputDirectory = outDir;
					parameters.Project = proj;
					parameters.Marker = new ObfAttrMarker();
				}

				int retVal = RunProject(parameters);

				if (NeedPause() && !noPause) {
					Console.WriteLine("Press any key to continue...");
					Console.ReadKey(true);
				}

				return retVal;
			}
			finally {
				Console.ForegroundColor = original;
				Console.Title = originalTitle;
			}
		}

		static int RunProject(ConfuserParameters parameters) {
			var logger = new ConsoleLogger();
			parameters.Logger = logger;

			Console.Title = "ConfuserEx - Running...";
			ConfuserEngine.Run(parameters).Wait();

			return logger.ReturnValue;
		}

		static bool NeedPause() {
			return Debugger.IsAttached || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPT"));
		}

		static void PrintUsage() {
			WriteLine("Usage:");
			WriteLine("Confuser.CLI -n|noPause <project configuration>");
			WriteLine("Confuser.CLI -n|noPause -o|out=<output directory> <modules>");
			WriteLine("    -n|noPause : no pause after finishing protection.");
			WriteLine("    -o|out     : specifies output directory.");
		}

		static void WriteLineWithColor(ConsoleColor color, string txt) {
			ConsoleColor original = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(txt);
			Console.ForegroundColor = original;
		}

		static void WriteLine(string txt) {
			Console.WriteLine(txt);
		}

		static void WriteLine() {
			Console.WriteLine();
		}

		class ConsoleLogger : ILogger {
			readonly DateTime begin;

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
				}
				else {
					Console.Title = "ConfuserEx - Fail";
					WriteLineWithColor(ConsoleColor.Red, "Failed " + timeString);
					ReturnValue = 1;
				}
			}
		}
	}
}