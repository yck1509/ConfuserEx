using System;
using System.Collections.Generic;
using System.IO;
using Confuser.Core.Project;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <summary>
	///     Base class of Confuser packers.
	/// </summary>
	/// <remarks>
	///     A parameterless constructor must exists in derived classes to enable plugin discovery.
	/// </remarks>
	public abstract class Packer : ConfuserComponent {
		/// <summary>
		///     Executes the packer.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="parameters">The parameters of packer.</param>
		protected internal abstract void Pack(ConfuserContext context, ProtectionParameters parameters);

		/// <summary>
		///     Protects the stub using original project settings replace the current output with the protected stub.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="fileName">The result file name.</param>
		/// <param name="module">The stub module.</param>
		/// <param name="snKey">The strong name key.</param>
		/// <param name="prot">The packer protection that applies to the stub.</param>
		protected void ProtectStub(ConfuserContext context, string fileName, byte[] module, StrongNameKey snKey, Protection prot = null) {
			string tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			string outDir = Path.Combine(tmpDir, Path.GetRandomFileName());
			Directory.CreateDirectory(tmpDir);

			for (int i = 0; i < context.OutputModules.Count; i++) {
				string path = Path.GetFullPath(Path.Combine(tmpDir, context.OutputPaths[i]));
				var dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				File.WriteAllBytes(path, context.OutputModules[i]);
			}
			File.WriteAllBytes(Path.Combine(tmpDir, fileName), module);

			var proj = new ConfuserProject();
			proj.Seed = context.Project.Seed;
			foreach (Rule rule in context.Project.Rules)
				proj.Rules.Add(rule);
			proj.Add(new ProjectModule {
				Path = fileName
			});
			proj.BaseDirectory = tmpDir;
			proj.OutputDirectory = outDir;
			foreach (var path in context.Project.ProbePaths)
				proj.ProbePaths.Add(path);
			proj.ProbePaths.Add(context.Project.BaseDirectory);

			PluginDiscovery discovery = null;
			if (prot != null) {
				var rule = new Rule {
					Preset = ProtectionPreset.None,
					Inherit = true,
					Pattern = "true"
				};
				rule.Add(new SettingItem<Protection> {
					Id = prot.Id,
					Action = SettingItemAction.Add
				});
				proj.Rules.Add(rule);
				discovery = new PackerDiscovery(prot);
			}

			try {
				ConfuserEngine.Run(new ConfuserParameters {
					Logger = new PackerLogger(context.Logger),
					PluginDiscovery = discovery,
					Marker = new PackerMarker(snKey),
					Project = proj,
					PackerInitiated = true
				}, context.token).Wait();
			}
			catch (AggregateException ex) {
				context.Logger.Error("Failed to protect packer stub.");
				throw new ConfuserException(ex);
			}

			context.OutputModules = new[] { File.ReadAllBytes(Path.Combine(outDir, fileName)) };
			context.OutputPaths = new[] { fileName };
		}
	}

	internal class PackerLogger : ILogger {
		readonly ILogger baseLogger;

		public PackerLogger(ILogger baseLogger) {
			this.baseLogger = baseLogger;
		}

		public void Debug(string msg) {
			baseLogger.Debug(msg);
		}

		public void DebugFormat(string format, params object[] args) {
			baseLogger.DebugFormat(format, args);
		}

		public void Info(string msg) {
			baseLogger.Info(msg);
		}

		public void InfoFormat(string format, params object[] args) {
			baseLogger.InfoFormat(format, args);
		}

		public void Warn(string msg) {
			baseLogger.Warn(msg);
		}

		public void WarnFormat(string format, params object[] args) {
			baseLogger.WarnFormat(format, args);
		}

		public void WarnException(string msg, Exception ex) {
			baseLogger.WarnException(msg, ex);
		}

		public void Error(string msg) {
			baseLogger.Error(msg);
		}

		public void ErrorFormat(string format, params object[] args) {
			baseLogger.ErrorFormat(format, args);
		}

		public void ErrorException(string msg, Exception ex) {
			baseLogger.ErrorException(msg, ex);
		}

		public void Progress(int progress, int overall) {
			baseLogger.Progress(progress, overall);
		}

		public void EndProgress() {
			baseLogger.EndProgress();
		}

		public void Finish(bool successful) {
			if (!successful)
				throw new ConfuserException(null);
			baseLogger.Info("Finish protecting packer stub.");
		}
	}

	internal class PackerMarker : Marker {
		readonly StrongNameKey snKey;

		public PackerMarker(StrongNameKey snKey) {
			this.snKey = snKey;
		}

		protected internal override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context) {
			MarkerResult result = base.MarkProject(proj, context);
			foreach (ModuleDefMD module in result.Modules)
				context.Annotations.Set(module, SNKey, snKey);
			return result;
		}
	}

	internal class PackerDiscovery : PluginDiscovery {
		readonly Protection prot;

		public PackerDiscovery(Protection prot) {
			this.prot = prot;
		}

		protected override void GetPluginsInternal(ConfuserContext context, IList<Protection> protections, IList<Packer> packers, IList<ConfuserComponent> components) {
			base.GetPluginsInternal(context, protections, packers, components);
			protections.Add(prot);
		}
	}
}