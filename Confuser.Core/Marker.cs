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
    using System.Reflection;
    using Rules = Dictionary<Rule, PatternExpression>;

	/// <summary>
	///     Resolves and marks the modules with protection settings according to the rules.
	/// </summary>
	public class Marker {
        // http://objectmix.com/dotnet/794418-reading-cryptographic-key-information-snk-assembly-print.html
	    private sealed class SnkUtil
	    {
	        private const int MagicPrivateIndex = 8;
	        private const int MagicPublicIndex = 20;
	        private const int MagicSize = 4;

	        private static byte[] GetFileBytes(string path)
	        {
	            Stream stm = File.OpenRead(path);
	            using (stm)
	            {
	                byte[] buffer = new byte[stm.Length];
	                stm.Read(buffer, 0, buffer.Length);
	                return buffer;
	            }
	        }

	        private static byte[] Copy(byte[] src, int index, int size)
	        {
	            if ((src == null) || (src.Length < (index + size)))
	                return null;

	            byte[] dest = new byte[size];
	            Array.Copy(src, index, dest, 0, size);
	            return dest;
	        }

	        private static bool Check(byte[] bytes, byte[] check, int index)
	        {
	            if (bytes == null)
	                throw (new ArgumentNullException("bytes"));
	            if (check == null)
	                throw (new ArgumentNullException("check"));
	            if (bytes.Length < index + check.Length)
	                throw (new ArgumentException("index plus check length outside bounds of bytes array."));

	            for (int i = 0; i < check.Length; i++)
	            {
	                if (bytes[i + index] != check[i])
	                    return false;
	            }
	            return true;
	        }

	        /// <summary>
	        /// Check that RSA1 is in header (public key only).
	        /// </summary>
	        /// <param name="keypair"></param>
	        /// <returns></returns>
	        private static bool CheckRSA1(byte[] bytes)
	        {
// Check that RSA1 is in header.
// R S A 1
	            byte[] check = new byte[] {0x52, 0x53, 0x41, 0x31};
	            return Check(bytes, check, MagicPublicIndex);
	        }

	        /// <summary>
	        /// Check that RSA2 is in header (public and private key).
	        /// </summary>
	        /// <param name="keypair"></param>
	        /// <returns></returns>
	        private static bool CheckRSA2(byte[] bytes)
	        {
// Check that RSA2 is in header.
// R S A 2
	            byte[] check = new byte[] {0x52, 0x53, 0x41, 0x32};
	            return Check(bytes, check, MagicPrivateIndex);
	        }

	        /// <summary>
	        /// Returns RSAParameters from byte[].
	        /// Example to get rsa public key from assembly:
	        /// byte[] pubkey =private System.Reflection.Assembly.GetExecutingAssembly ().private GetName().private GetPublicKey();

	        /// RSAParameters p = SnkUtil.GetRSAParameters(pubkey);
	        /// </summary>
	        /// <param name="keypair"></param>
	        /// <returns></returns>
	        private static RSAParameters GetRSAParameters(byte[] bytes)
	        {
	            if ((bytes == null) || (bytes.Length == 0))
	                throw new ArgumentNullException("bytes");

	            bool pubonly = (bytes.Length == 160);

	            if (pubonly && !CheckRSA1(bytes))
	                return new RSAParameters();

	            if (!pubonly && !CheckRSA2(bytes))
	                return new RSAParameters();

	            RSAParameters parameters = new RSAParameters();

	            int index = pubonly ? MagicPublicIndex : MagicPrivateIndex;
	            index += MagicSize + 4;
	            int size = 4;
	            parameters.Exponent = Copy(bytes, index, size);
	            Array.Reverse(parameters.Exponent);

	            index += size;
	            size = 128;
	            parameters.Modulus = Copy(bytes, index, size);
	            Array.Reverse(parameters.Modulus);

	            if (pubonly)
	                return parameters;

// Figure private params
// Must reverse order (little vs. big endian issue)

	            index += size;
	            size = 64;
	            parameters.P = Copy(bytes, index, size);
	            Array.Reverse(parameters.P);

	            index += size;
	            size = 64;
	            parameters.Q = Copy(bytes, index, size);
	            Array.Reverse(parameters.Q);

	            index += size;
	            size = 64;
	            parameters.DP = Copy(bytes, index, size);
	            Array.Reverse(parameters.DP);

	            index += size;
	            size = 64;
	            parameters.DQ = Copy(bytes, index, size);
	            Array.Reverse(parameters.DQ);

	            index += size;
	            size = 64;
	            parameters.InverseQ = Copy(bytes, index, size);
	            Array.Reverse(parameters.InverseQ);

	            index += size;
	            size = 128;
	            parameters.D = Copy(bytes, index, size);
	            Array.Reverse(parameters.D);

	            return parameters;
	        }

	        private static RSACryptoServiceProvider GetRsaProvider(byte[] bytes)
	        {
	            if (bytes == null)
	                throw new ArgumentNullException("bytes");

	            RSAParameters parameters = GetRSAParameters(bytes);

// Must set KeyNumber to AT_SIGNATURE for strong
// name keypair to be correctly imported.
//	CspParameters cp = new CspParameters();
//	cp.KeyNumber = 2; // AT_SIGNATURE

//	RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(1024, cp);
	            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
	            rsa.ImportParameters(parameters);
	            return rsa;
	        }

	        /// <summary>
	        /// Returns RSA object from *.snk key file.
	        /// </summary>
	        /// <param name="path">Path to snk file.</param>
	        /// <returns>RSACryptoServiceProvider</returns>
	        public static RSACryptoServiceProvider GetRsaProvider(string path)
	        {
	            if (path == null)
	                throw new ArgumentNullException("path");

	            byte[] bytes = GetFileBytes(path);
	            if (bytes == null)
	                throw new Exception("Invalid SNK file.");

	            RSACryptoServiceProvider rsa = GetRsaProvider(bytes);
	            return rsa;
	        }

	        public static RSACryptoServiceProvider GetRsaProvider(Assembly assembly)
	        {
	            if (assembly == null)
	                throw new ArgumentNullException("assembly");

	            byte[] bytes = assembly.GetName().GetPublicKey();
	            if (bytes.Length == 0)
	                throw new Exception("No public key in assembly.");

	            RSAParameters parameters = GetRSAParameters(bytes);
	            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
	            rsa.ImportParameters(parameters);
	            return rsa;
	        }
	    }

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
				if (prot.Preset <= preset && !settings.ContainsKey(prot))
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

			try
			{
				if (pass != null) //pfx / snk
				{
                    // snk
                    var extension = Path.GetExtension(path);
				    if (string.Equals(extension, ".snk", StringComparison.InvariantCulture))
				    {
				        var snkRsa = SnkUtil.GetRsaProvider(path);
				        return new StrongNameKey(snkRsa.ExportCspBlob(true));
				    }

                    // pfx
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