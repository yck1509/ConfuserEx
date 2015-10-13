using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Win32;
using InformationalAttribute = System.Reflection.AssemblyInformationalVersionAttribute;
using ProductAttribute = System.Reflection.AssemblyProductAttribute;
using CopyrightAttribute = System.Reflection.AssemblyCopyrightAttribute;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Confuser.Core {
	/// <summary>
	///     The processing engine of ConfuserEx.
	/// </summary>
	public static class ConfuserEngine {
		/// <summary>
		///     The version of ConfuserEx.
		/// </summary>
		public static readonly string Version;

		static readonly string Copyright;

		static ConfuserEngine() {
			Assembly assembly = typeof(ConfuserEngine).Assembly;
			var nameAttr = (ProductAttribute)assembly.GetCustomAttributes(typeof(ProductAttribute), false)[0];
			var verAttr = (InformationalAttribute)assembly.GetCustomAttributes(typeof(InformationalAttribute), false)[0];
			var cpAttr = (CopyrightAttribute)assembly.GetCustomAttributes(typeof(CopyrightAttribute), false)[0];
			Version = string.Format("{0} {1}", nameAttr.Product, verAttr.InformationalVersion);
			Copyright = cpAttr.Copyright;

			AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
				try {
					var asmName = new AssemblyName(e.Name);
					foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
						if (asm.GetName().Name == asmName.Name)
							return asm;
					return null;
				}
				catch {
					return null;
				}
			};
		}

		/// <summary>
		///     Runs the engine with the specified parameters.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="token">The token used for cancellation.</param>
		/// <returns>Task to run the engine.</returns>
		/// <exception cref="System.ArgumentNullException">
		///     <paramref name="parameters" />.Project is <c>null</c>.
		/// </exception>
		public static Task Run(ConfuserParameters parameters, CancellationToken? token = null) {
			if (parameters.Project == null)
				throw new ArgumentNullException("parameters");
			if (token == null)
				token = new CancellationTokenSource().Token;
			return Task.Factory.StartNew(() => RunInternal(parameters, token.Value), token.Value);
		}

		/// <summary>
		///     Runs the engine.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="token">The cancellation token.</param>
		static void RunInternal(ConfuserParameters parameters, CancellationToken token) {
			// 1. Setup context
			var context = new ConfuserContext();
			context.Logger = parameters.GetLogger();
			context.Project = parameters.Project.Clone();
			context.PackerInitiated = parameters.PackerInitiated;
			context.token = token;

			PrintInfo(context);

			bool ok = false;
			try {
				var asmResolver = new AssemblyResolver();
				asmResolver.EnableTypeDefCache = true;
				asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);
				context.Resolver = asmResolver;
				context.BaseDirectory = Path.Combine(Environment.CurrentDirectory, parameters.Project.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
				context.OutputDirectory = Path.Combine(parameters.Project.BaseDirectory, parameters.Project.OutputDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
				foreach (string probePath in parameters.Project.ProbePaths)
					asmResolver.PostSearchPaths.Insert(0, Path.Combine(context.BaseDirectory, probePath));

				context.CheckCancellation();

				Marker marker = parameters.GetMarker();

				// 2. Discover plugins
				context.Logger.Debug("Discovering plugins...");

				IList<Protection> prots;
				IList<Packer> packers;
				IList<ConfuserComponent> components;
				parameters.GetPluginDiscovery().GetPlugins(context, out prots, out packers, out components);

				context.Logger.InfoFormat("Discovered {0} protections, {1} packers.", prots.Count, packers.Count);

				context.CheckCancellation();

				// 3. Resolve dependency
				context.Logger.Debug("Resolving component dependency...");
				try {
					var resolver = new DependencyResolver(prots);
					prots = resolver.SortDependency();
				}
				catch (CircularDependencyException ex) {
					context.Logger.ErrorException("", ex);
					throw new ConfuserException(ex);
				}

				components.Insert(0, new CoreComponent(parameters, marker));
				foreach (Protection prot in prots)
					components.Add(prot);
				foreach (Packer packer in packers)
					components.Add(packer);

				context.CheckCancellation();

				// 4. Load modules
				context.Logger.Info("Loading input modules...");
				marker.Initalize(prots, packers);
				MarkerResult markings = marker.MarkProject(parameters.Project, context);
				context.Modules = markings.Modules.ToList().AsReadOnly();
				foreach (var module in context.Modules)
					module.EnableTypeDefFindCache = false;
				context.OutputModules = Enumerable.Repeat<byte[]>(null, markings.Modules.Count).ToArray();
				context.OutputSymbols = Enumerable.Repeat<byte[]>(null, markings.Modules.Count).ToArray();
				context.OutputPaths = Enumerable.Repeat<string>(null, markings.Modules.Count).ToArray();
				context.Packer = markings.Packer;
				context.ExternalModules = markings.ExternalModules;

				context.CheckCancellation();

				// 5. Initialize components
				context.Logger.Info("Initializing...");
				foreach (ConfuserComponent comp in components) {
					try {
						comp.Initialize(context);
					}
					catch (Exception ex) {
						context.Logger.ErrorException("Error occured during initialization of '" + comp.Name + "'.", ex);
						throw new ConfuserException(ex);
					}
					context.CheckCancellation();
				}

				context.CheckCancellation();

				// 6. Build pipeline
				context.Logger.Debug("Building pipeline...");
				var pipeline = new ProtectionPipeline();
				context.Pipeline = pipeline;
				foreach (ConfuserComponent comp in components) {
					comp.PopulatePipeline(pipeline);
				}

				context.CheckCancellation();

				//7. Run pipeline
				RunPipeline(pipeline, context);

				ok = true;
			}
			catch (AssemblyResolveException ex) {
				context.Logger.ErrorException("Failed to resolve an assembly, check if all dependencies are present in the correct version.", ex);
				PrintEnvironmentInfo(context);
			}
			catch (TypeResolveException ex) {
                context.Logger.ErrorException("Failed to resolve a type, check if all dependencies are present in the correct version.", ex);
				PrintEnvironmentInfo(context);
			}
			catch (MemberRefResolveException ex) {
				context.Logger.ErrorException("Failed to resolve a member, check if all dependencies are present in the correct version.", ex);
				PrintEnvironmentInfo(context);
			}
			catch (IOException ex) {
				context.Logger.ErrorException("An IO error occurred, check if all input/output locations are readable/writable.", ex);
			}
			catch (OperationCanceledException) {
				context.Logger.Error("Operation cancelled.");
			}
			catch (ConfuserException) {
				// Exception is already handled/logged, so just ignore and report failure
			}
			catch (Exception ex) {
				context.Logger.ErrorException("Unknown error occurred.", ex);
			}
			finally {
				if (context.Resolver != null)
					context.Resolver.Clear();
				context.Logger.Finish(ok);
			}
		}

		/// <summary>
		///     Runs the protection pipeline.
		/// </summary>
		/// <param name="pipeline">The protection pipeline.</param>
		/// <param name="context">The context.</param>
		static void RunPipeline(ProtectionPipeline pipeline, ConfuserContext context) {
			Func<IList<IDnlibDef>> getAllDefs = () => context.Modules.SelectMany(module => module.FindDefinitions()).ToList();
			Func<ModuleDef, IList<IDnlibDef>> getModuleDefs = module => module.FindDefinitions().ToList();

			context.CurrentModuleIndex = -1;

			pipeline.ExecuteStage(PipelineStage.Inspection, Inspection, () => getAllDefs(), context);

			var options = new ModuleWriterOptionsBase[context.Modules.Count];
			var listeners = new ModuleWriterListener[context.Modules.Count];
			for (int i = 0; i < context.Modules.Count; i++) {
				context.CurrentModuleIndex = i;
				context.CurrentModuleWriterOptions = null;
				context.CurrentModuleWriterListener = null;

				pipeline.ExecuteStage(PipelineStage.BeginModule, BeginModule, () => getModuleDefs(context.CurrentModule), context);
				pipeline.ExecuteStage(PipelineStage.ProcessModule, ProcessModule, () => getModuleDefs(context.CurrentModule), context);
				pipeline.ExecuteStage(PipelineStage.OptimizeMethods, OptimizeMethods, () => getModuleDefs(context.CurrentModule), context);
				pipeline.ExecuteStage(PipelineStage.EndModule, EndModule, () => getModuleDefs(context.CurrentModule), context);

				options[i] = context.CurrentModuleWriterOptions;
				listeners[i] = context.CurrentModuleWriterListener;
			}

			for (int i = 0; i < context.Modules.Count; i++) {
				context.CurrentModuleIndex = i;
				context.CurrentModuleWriterOptions = options[i];
				context.CurrentModuleWriterListener = listeners[i];

				pipeline.ExecuteStage(PipelineStage.WriteModule, WriteModule, () => getModuleDefs(context.CurrentModule), context);

				context.OutputModules[i] = context.CurrentModuleOutput;
				context.OutputSymbols[i] = context.CurrentModuleSymbol;
				context.CurrentModuleWriterOptions = null;
				context.CurrentModuleWriterListener = null;
				context.CurrentModuleOutput = null;
				context.CurrentModuleSymbol = null;
			}

			context.CurrentModuleIndex = -1;

			pipeline.ExecuteStage(PipelineStage.Debug, Debug, () => getAllDefs(), context);
			pipeline.ExecuteStage(PipelineStage.Pack, Pack, () => getAllDefs(), context);
			pipeline.ExecuteStage(PipelineStage.SaveModules, SaveModules, () => getAllDefs(), context);

			if (!context.PackerInitiated)
				context.Logger.Info("Done.");
		}

		static void Inspection(ConfuserContext context) {
			context.Logger.Info("Resolving dependencies...");
			foreach (var dependency in context.Modules
			                                  .SelectMany(module => module.GetAssemblyRefs().Select(asmRef => Tuple.Create(asmRef, module)))) {
				try {
					AssemblyDef assembly = context.Resolver.ResolveThrow(dependency.Item1, dependency.Item2);
				}
				catch (AssemblyResolveException ex) {
					context.Logger.ErrorException("Failed to resolve dependency of '" + dependency.Item2.Name + "'.", ex);
					throw new ConfuserException(ex);
				}
			}

			context.Logger.Debug("Checking Strong Name...");
			foreach (ModuleDefMD module in context.Modules) {
				var snKey = context.Annotations.Get<StrongNameKey>(module, Marker.SNKey);
				if (snKey == null && module.IsStrongNameSigned)
					context.Logger.WarnFormat("[{0}] SN Key is not provided for a signed module, the output may not be working.", module.Name);
				else if (snKey != null && !module.IsStrongNameSigned)
					context.Logger.WarnFormat("[{0}] SN Key is provided for an unsigned module, the output may not be working.", module.Name);
				else if (snKey != null && module.IsStrongNameSigned &&
				         !module.Assembly.PublicKey.Data.SequenceEqual(snKey.PublicKey))
					context.Logger.WarnFormat("[{0}] Provided SN Key and signed module's public key do not match, the output may not be working.", module.Name);
			}

			var marker = context.Registry.GetService<IMarkerService>();

			context.Logger.Debug("Creating global .cctors...");
			foreach (ModuleDefMD module in context.Modules) {
				TypeDef modType = module.GlobalType;
				if (modType == null) {
					modType = new TypeDefUser("", "<Module>", null);
					modType.Attributes = TypeAttributes.AnsiClass;
					module.Types.Add(modType);
					marker.Mark(modType, null);
				}
				MethodDef cctor = modType.FindOrCreateStaticConstructor();
				if (!marker.IsMarked(cctor))
					marker.Mark(cctor, null);
			}

			context.Logger.Debug("Watermarking...");
			foreach (ModuleDefMD module in context.Modules) {
				TypeRef attrRef = module.CorLibTypes.GetTypeRef("System", "Attribute");
				var attrType = new TypeDefUser("", "ConfusedByAttribute", attrRef);
				module.Types.Add(attrType);
				marker.Mark(attrType, null);

				var ctor = new MethodDefUser(
					".ctor",
					MethodSig.CreateInstance(module.CorLibTypes.Void, module.CorLibTypes.String),
					MethodImplAttributes.Managed,
					MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
				ctor.Body = new CilBody();
				ctor.Body.MaxStack = 1;
				ctor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
				ctor.Body.Instructions.Add(OpCodes.Call.ToInstruction(new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), attrRef)));
				ctor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
				attrType.Methods.Add(ctor);
				marker.Mark(ctor, null);

				var attr = new CustomAttribute(ctor);
				attr.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.String, Version));

				module.CustomAttributes.Add(attr);
			}
		}

		static void CopyPEHeaders(PEHeadersOptions writerOptions, ModuleDefMD module) {
			var image = module.MetaData.PEImage;
			writerOptions.MajorImageVersion = image.ImageNTHeaders.OptionalHeader.MajorImageVersion;
			writerOptions.MajorLinkerVersion = image.ImageNTHeaders.OptionalHeader.MajorLinkerVersion;
			writerOptions.MajorOperatingSystemVersion = image.ImageNTHeaders.OptionalHeader.MajorOperatingSystemVersion;
			writerOptions.MajorSubsystemVersion = image.ImageNTHeaders.OptionalHeader.MajorSubsystemVersion;
			writerOptions.MinorImageVersion = image.ImageNTHeaders.OptionalHeader.MinorImageVersion;
			writerOptions.MinorLinkerVersion = image.ImageNTHeaders.OptionalHeader.MinorLinkerVersion;
			writerOptions.MinorOperatingSystemVersion = image.ImageNTHeaders.OptionalHeader.MinorOperatingSystemVersion;
			writerOptions.MinorSubsystemVersion = image.ImageNTHeaders.OptionalHeader.MinorSubsystemVersion;
		}

		static void BeginModule(ConfuserContext context) {
			context.Logger.InfoFormat("Processing module '{0}'...", context.CurrentModule.Name);

			context.CurrentModuleWriterListener = new ModuleWriterListener();
			context.CurrentModuleWriterListener.OnWriterEvent += (sender, e) => context.CheckCancellation();
			context.CurrentModuleWriterOptions = new ModuleWriterOptions(context.CurrentModule, context.CurrentModuleWriterListener);
			CopyPEHeaders(context.CurrentModuleWriterOptions.PEHeadersOptions, context.CurrentModule);

			if (!context.CurrentModule.IsILOnly || context.CurrentModule.VTableFixups != null)
				context.RequestNative();
			
			var snKey = context.Annotations.Get<StrongNameKey>(context.CurrentModule, Marker.SNKey);
			context.CurrentModuleWriterOptions.InitializeStrongNameSigning(context.CurrentModule, snKey);

			foreach (TypeDef type in context.CurrentModule.GetTypes())
				foreach (MethodDef method in type.Methods) {
					if (method.Body != null) {
						method.Body.Instructions.SimplifyMacros(method.Body.Variables, method.Parameters);
					}
				}
		}

		static void ProcessModule(ConfuserContext context) { }

		static void OptimizeMethods(ConfuserContext context) {
			foreach (TypeDef type in context.CurrentModule.GetTypes())
				foreach (MethodDef method in type.Methods) {
					if (method.Body != null)
						method.Body.Instructions.OptimizeMacros();
				}
		}

		static void EndModule(ConfuserContext context) {
			string output = context.Modules[context.CurrentModuleIndex].Location;
			if (output != null) {
				if (!Path.IsPathRooted(output))
					output = Path.Combine(Environment.CurrentDirectory, output);
				output = Utils.GetRelativePath(output, context.BaseDirectory);
			}
			else {
				output = context.CurrentModule.Name;
			}
			context.OutputPaths[context.CurrentModuleIndex] = output;
		}

		static void WriteModule(ConfuserContext context) {
			context.Logger.InfoFormat("Writing module '{0}'...", context.CurrentModule.Name);

			MemoryStream pdb = null, output = new MemoryStream();

			if (context.CurrentModule.PdbState != null) {
				pdb = new MemoryStream();
				context.CurrentModuleWriterOptions.WritePdb = true;
				context.CurrentModuleWriterOptions.PdbFileName = Path.ChangeExtension(Path.GetFileName(context.OutputPaths[context.CurrentModuleIndex]), "pdb");
				context.CurrentModuleWriterOptions.PdbStream = pdb;
			}

			if (context.CurrentModuleWriterOptions is ModuleWriterOptions)
				context.CurrentModule.Write(output, (ModuleWriterOptions)context.CurrentModuleWriterOptions);
			else
				context.CurrentModule.NativeWrite(output, (NativeModuleWriterOptions)context.CurrentModuleWriterOptions);

			context.CurrentModuleOutput = output.ToArray();
			if (context.CurrentModule.PdbState != null)
				context.CurrentModuleSymbol = pdb.ToArray();
		}

		static void Debug(ConfuserContext context) {
			context.Logger.Info("Finalizing...");
			for (int i = 0; i < context.OutputModules.Count; i++) {
				if (context.OutputSymbols[i] == null)
					continue;
				string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				File.WriteAllBytes(Path.ChangeExtension(path, "pdb"), context.OutputSymbols[i]);
			}
		}

		static void Pack(ConfuserContext context) {
			if (context.Packer != null) {
				context.Logger.Info("Packing...");
				context.Packer.Pack(context, new ProtectionParameters(context.Packer, context.Modules.OfType<IDnlibDef>().ToList()));
			}
		}

		static void SaveModules(ConfuserContext context) {
			context.Resolver.Clear();
			for (int i = 0; i < context.OutputModules.Count; i++) {
				string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
				string dir = Path.GetDirectoryName(path);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				context.Logger.DebugFormat("Saving to '{0}'...", path);
				File.WriteAllBytes(path, context.OutputModules[i]);
			}
		}

		/// <summary>
		///     Prints the copyright stuff and environment information.
		/// </summary>
		/// <param name="context">The working context.</param>
		static void PrintInfo(ConfuserContext context) {
			if (context.PackerInitiated) {
				context.Logger.Info("Protecting packer stub...");
			}
			else {
				context.Logger.InfoFormat("{0} {1}", Version, Copyright);

				Type mono = Type.GetType("Mono.Runtime");
				context.Logger.InfoFormat("Running on {0}, {1}, {2} bits",
				                          Environment.OSVersion,
				                          mono == null ?
					                          ".NET Framework v" + Environment.Version :
					                          mono.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null),
				                          IntPtr.Size * 8);
			}
		}

		static IEnumerable<string> GetFrameworkVersions() {
			// http://msdn.microsoft.com/en-us/library/hh925568.aspx

			using (RegistryKey ndpKey =
				RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").
				            OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\")) {
				foreach (string versionKeyName in ndpKey.GetSubKeyNames()) {
					if (!versionKeyName.StartsWith("v"))
						continue;

					RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
					var name = (string)versionKey.GetValue("Version", "");
					string sp = versionKey.GetValue("SP", "").ToString();
					string install = versionKey.GetValue("Install", "").ToString();
					if (install == "" || sp != "" && install == "1")
						yield return versionKeyName + "  " + name;

					if (name != "")
						continue;

					foreach (string subKeyName in versionKey.GetSubKeyNames()) {
						RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
						name = (string)subKey.GetValue("Version", "");
						if (name != "")
							sp = subKey.GetValue("SP", "").ToString();
						install = subKey.GetValue("Install", "").ToString();

						if (install == "")
							yield return versionKeyName + "  " + name;
						else if (install == "1")
							yield return "  " + subKeyName + "  " + name;
					}
				}
			}

			using (RegistryKey ndpKey =
				RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").
				            OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\")) {
				if (ndpKey.GetValue("Release") == null)
					yield break;
				var releaseKey = (int)ndpKey.GetValue("Release");
				yield return "v4.5 " + releaseKey;
			}
		}

		/// <summary>
		///     Prints the environment information when error occurred.
		/// </summary>
		/// <param name="context">The working context.</param>
		static void PrintEnvironmentInfo(ConfuserContext context) {
			if (context.PackerInitiated)
				return;

			context.Logger.Error("---BEGIN DEBUG INFO---");

			context.Logger.Error("Installed Framework Versions:");
			foreach (string ver in GetFrameworkVersions()) {
				context.Logger.ErrorFormat("    {0}", ver.Trim());
			}
			context.Logger.Error("");

			if (context.Resolver != null) {
				context.Logger.Error("Cached assemblies:");
				foreach (AssemblyDef asm in context.Resolver.GetCachedAssemblies()) {
					if (string.IsNullOrEmpty(asm.ManifestModule.Location))
						context.Logger.ErrorFormat("    {0}", asm.FullName);
					else
						context.Logger.ErrorFormat("    {0} ({1})", asm.FullName, asm.ManifestModule.Location);
					foreach (var reference in asm.Modules.OfType<ModuleDefMD>().SelectMany(m => m.GetAssemblyRefs()))
						context.Logger.ErrorFormat("        {0}", reference.FullName);
				}
			}

			context.Logger.Error("---END DEBUG INFO---");
		}
	}
}