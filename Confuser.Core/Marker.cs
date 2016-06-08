using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Confuser.Core.Project;
using Confuser.Core.Project.Patterns;
using dnlib.DotNet;

namespace Confuser.Core {
	using Rules = Dictionary<Rule, PatternExpression>;

	/// <summary>
	///     Resolves and marks the modules with protection settings according to the rules.
	/// </summary>
	public class Marker {
		/// <summary>
		///     Annotation key of Strong Name Key.
		/// </summary>
		public static readonly object SNKey = new object();

		/// <summary>
		///     Annotation key of rules.
		/// </summary>
		public static readonly object RulesKey = new object();

		/// <summary>
		///     The packers available to use.
		/// </summary>
		protected Dictionary<string, Packer> packers;

		/// <summary>
		///     The protections available to use.
		/// </summary>
		protected Dictionary<string, Protection> protections;

		/// <summary>
		///     Initalizes the Marker with specified protections and packers.
		/// </summary>
		/// <param name="protections">The protections.</param>
		/// <param name="packers">The packers.</param>
		public virtual void Initalize(IList<Protection> protections, IList<Packer> packers) {
			this.protections = protections.ToDictionary(prot => prot.Id, prot => prot, StringComparer.OrdinalIgnoreCase);
			this.packers = packers.ToDictionary(packer => packer.Id, packer => packer, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		///     Fills the protection settings with the specified preset.
		/// </summary>
		/// <param name="preset">The preset.</param>
		/// <param name="settings">The settings.</param>
		void FillPreset(ProtectionPreset preset, ProtectionSettings settings) {
			foreach (Protection prot in protections.Values)
				if (prot.Preset != ProtectionPreset.None && prot.Preset <= preset && !settings.ContainsKey(prot))
					settings.Add(prot, new Dictionary<string, string>());
		}

		/// <summary>
		///     Loads the Strong Name Key at the specified path with a optional password.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="path">The path to the key.</param>
		/// <param name="pass">
		///     The password of the certificate at <paramref name="path" /> if
		///     it is a pfx file; otherwise, <c>null</c>.
		/// </param>
		/// <returns>The loaded Strong Name Key.</returns>
		public static StrongNameKey LoadSNKey(ConfuserContext context, string path, string pass) {
			if (path == null) return null;

			try {
				if (pass != null) //pfx
				{
					// http://stackoverflow.com/a/12196742/462805
					var cert = new X509Certificate2();
					cert.Import(path, pass, X509KeyStorageFlags.Exportable);

					var rsa = cert.PrivateKey as RSACryptoServiceProvider;
					if (rsa == null)
						throw new ArgumentException("RSA key does not present in the certificate.", "path");

					return new StrongNameKey(rsa.ExportCspBlob(true));
				}
				return new StrongNameKey(path);
			}
			catch (Exception ex) {
				context.Logger.ErrorException("Cannot load the Strong Name Key located at: " + path, ex);
				throw new ConfuserException(ex);
			}
		}

		/// <summary>
		///     Loads the assembly and marks the project.
		/// </summary>
		/// <param name="proj">The project.</param>
		/// <param name="context">The working context.</param>
		/// <returns><see cref="MarkerResult" /> storing the marked modules and packer information.</returns>
		protected internal virtual MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context) {
			Packer packer = null;
			Dictionary<string, string> packerParams = null;

			if (proj.Packer != null) {
				if (!packers.ContainsKey(proj.Packer.Id)) {
					context.Logger.ErrorFormat("Cannot find packer with ID '{0}'.", proj.Packer.Id);
					throw new ConfuserException(null);
				}
				if (proj.Debug)
					context.Logger.Warn("Generated Debug symbols might not be usable with packers!");

				packer = packers[proj.Packer.Id];
				packerParams = new Dictionary<string, string>(proj.Packer, StringComparer.OrdinalIgnoreCase);
			}

			var modules = new List<Tuple<ProjectModule, ModuleDefMD>>();
			var extModules = new List<byte[]>();
			foreach (ProjectModule module in proj) {
				if (module.IsExternal) {
					extModules.Add(module.LoadRaw(proj.BaseDirectory));
					continue;
				}

				ModuleDefMD modDef = module.Resolve(proj.BaseDirectory, context.Resolver.DefaultModuleContext);
				context.CheckCancellation();

				if (proj.Debug)
					modDef.LoadPdb();

				context.Resolver.AddToCache(modDef);
				modules.Add(Tuple.Create(module, modDef));
			}

			foreach (var module in modules) {
				context.Logger.InfoFormat("Loading '{0}'...", module.Item1.Path);
				Rules rules = ParseRules(proj, module.Item1, context);

				context.Annotations.Set(module.Item2, SNKey, LoadSNKey(context, module.Item1.SNKeyPath == null ? null : Path.Combine(proj.BaseDirectory, module.Item1.SNKeyPath), module.Item1.SNKeyPassword));
				context.Annotations.Set(module.Item2, RulesKey, rules);

				foreach (IDnlibDef def in module.Item2.FindDefinitions()) {
					ApplyRules(context, def, rules);
					context.CheckCancellation();
				}

				// Packer parameters are stored in modules
				if (packerParams != null)
					ProtectionParameters.GetParameters(context, module.Item2)[packer] = packerParams;
			}
			return new MarkerResult(modules.Select(module => module.Item2).ToList(), packer, extModules);
		}

		/// <summary>
		///     Marks the member definition.
		/// </summary>
		/// <param name="member">The member definition.</param>
		/// <param name="context">The working context.</param>
		protected internal virtual void MarkMember(IDnlibDef member, ConfuserContext context) {
			ModuleDef module = ((IMemberRef)member).Module;
			var rules = context.Annotations.Get<Rules>(module, RulesKey);
			ApplyRules(context, member, rules);
		}

		/// <summary>
		///     Parses the rules' patterns.
		/// </summary>
		/// <param name="proj">The project.</param>
		/// <param name="module">The module description.</param>
		/// <param name="context">The working context.</param>
		/// <returns>Parsed rule patterns.</returns>
		/// <exception cref="System.ArgumentException">
		///     One of the rules has invalid pattern.
		/// </exception>
		protected Rules ParseRules(ConfuserProject proj, ProjectModule module, ConfuserContext context) {
			var ret = new Rules();
			var parser = new PatternParser();
			foreach (Rule rule in proj.Rules.Concat(module.Rules)) {
				try {
					ret.Add(rule, parser.Parse(rule.Pattern));
				}
				catch (InvalidPatternException ex) {
					context.Logger.ErrorFormat("Invalid rule pattern: " + rule.Pattern + ".", ex);
					throw new ConfuserException(ex);
				}
				foreach (var setting in rule) {
					if (!protections.ContainsKey(setting.Id)) {
						context.Logger.ErrorFormat("Cannot find protection with ID '{0}'.", setting.Id);
						throw new ConfuserException(null);
					}
				}
			}
			return ret;
		}

		/// <summary>
		///     Applies the rules to the target definition.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="target">The target definition.</param>
		/// <param name="rules">The rules.</param>
		/// <param name="baseSettings">The base settings.</param>
		protected void ApplyRules(ConfuserContext context, IDnlibDef target, Rules rules, ProtectionSettings baseSettings = null) {
			var ret = baseSettings == null ? new ProtectionSettings() : new ProtectionSettings(baseSettings);
			foreach (var i in rules) {
				if (!(bool)i.Value.Evaluate(target)) continue;

				if (!i.Key.Inherit)
					ret.Clear();

				FillPreset(i.Key.Preset, ret);
				foreach (var prot in i.Key) {
					if (prot.Action == SettingItemAction.Add)
						ret[protections[prot.Id]] = new Dictionary<string, string>(prot, StringComparer.OrdinalIgnoreCase);
					else
						ret.Remove(protections[prot.Id]);
				}
			}

			ProtectionParameters.SetParameters(context, target, ret);
		}
	}
}