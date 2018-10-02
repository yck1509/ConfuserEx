using System;
using System.Diagnostics;
using System.IO;

public static class Program {
	public static int Main(string[] args) {
		if (args.Length != 1) {
			Console.WriteLine("invalid argument length.");
			return -1;
		}

		string dir = args[0];
		string ver = File.ReadAllText(Path.Combine(dir, "VERSION"));
		string tag = null;

		string gitDir = Path.Combine(dir, ".git");
		if (!Directory.Exists(gitDir)) {
			Console.WriteLine("git repository not found.");
		}
		else {
			try {
				var info = new ProcessStartInfo("git", "describe");
				info.RedirectStandardOutput = true;
				info.UseShellExecute = false;
				using (Process ps = Process.Start(info)) {
					tag = ps.StandardOutput.ReadLine();
					string[] infos = tag.Split('-');
					if (infos.Length >= 3)
						ver = ver + "." + infos[infos.Length - 2];
					else
						ver = infos[0].Substring(1);
					ps.WaitForExit();
					if (ps.ExitCode != 0) {
						Console.WriteLine("error when executing git describe: " + ps.ExitCode);
					}
				}
			}
			catch {
				Console.WriteLine("error when executing git describe.");
			}
		}
		tag = tag ?? "v" + ver;

		string template = Path.Combine(dir, "GlobalAssemblyInfo.Template.cs");
		string output = Path.Combine(dir, "GlobalAssemblyInfo.cs");

		string verInfo = File.ReadAllText(template);
		verInfo = verInfo.Replace("{{VER}}", ver);
		verInfo = verInfo.Replace("{{TAG}}", tag);
		File.WriteAllText(output, verInfo);
		Console.WriteLine("Version updated.");
		return 0;
	}
}