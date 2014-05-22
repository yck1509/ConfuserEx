using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.IO;
using dnlib.DotNet.Writer;
using Confuser.Core.Services;

namespace Confuser.Core
{
    /// <summary>
    /// The processing engine of Confuser.
    /// </summary>
    public static class ConfuserEngine
    {
        /// <summary>
        /// Runs the engine with the specified parameters.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        /// <param name="token">The token used for cancellation.</param>
        /// <returns>Task to run the engine.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="parameters"/>.Project is <c>null</c>.
        /// </exception>
        public static Task Run(ConfuserParameters parameters, CancellationToken? token = null)
        {
            if (parameters.Project == null)
                throw new ArgumentNullException("parameters");
            if (token == null)
                token = new CancellationTokenSource().Token;
            return Task.Factory.StartNew(() => RunInternal(parameters, token.Value), token.Value);
        }

        /// <summary>
        /// Runs the engine.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        /// <param name="token">The cancellation token.</param>
        static void RunInternal(ConfuserParameters parameters, CancellationToken token)
        {
            // 1. Setup context
            ConfuserContext context = new ConfuserContext();
            context.Logger = parameters.GetLogger();
            context.Project = parameters.Project;
            context.token = token;
            var asmResolver = new AssemblyResolver(); 
            asmResolver.EnableTypeDefCache = true;
            asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);
            foreach (var probePath in parameters.Project.ProbePaths)
                asmResolver.PostSearchPaths.Add(probePath);
            context.Resolver = asmResolver;
            context.BaseDirectory = Path.Combine(Environment.CurrentDirectory, parameters.Project.BaseDirectory + "\\");
            context.OutputDirectory = Path.Combine(parameters.Project.BaseDirectory, parameters.Project.OutputDirectory + "\\");

            PrintInfo(context);

            bool ok = false;
            try
            {
                Marker marker = parameters.GetMarker();

                // 2. Discover plugins
                context.Logger.Debug("Discovering plugins...");

                IList<Protection> prots;
                IList<Packer> packers;
                IList<ConfuserComponent> components;
                parameters.GetPluginDiscovery().GetPlugins(context, out prots, out packers, out components);

                context.Logger.InfoFormat("Discovered {0} protections, {1} packers.", prots.Count, packers.Count);

                // 3. Resolve dependency
                context.Logger.Debug("Resolving component dependency...");
                try
                {
                    var resolver = new DependencyResolver(prots);
                    prots = resolver.SortDependency();
                }
                catch (CircularDependencyException ex)
                {
                    context.Logger.ErrorException("", ex);
                    throw new ConfuserException(ex);
                }

                components.Insert(0, new CoreComponent(parameters, marker));
                foreach (var prot in prots)
                    components.Add(prot);
                foreach (var packer in packers)
                    components.Add(packer);

                // 4. Load modules
                context.Logger.Info("Loading input modules...");
                marker.Initalize(prots, packers);
                var markings = marker.MarkProject(parameters.Project, context);
                context.Modules = markings.Modules.ToList().AsReadOnly();
                context.OutputModules = Enumerable.Repeat<byte[]>(null, markings.Modules.Count).ToArray();
                context.OutputPaths = Enumerable.Repeat<string>(null, markings.Modules.Count).ToArray();
                foreach (var module in context.Modules)
                    asmResolver.AddToCache(module);
                context.Packer = markings.Packer;

                // 5. Initialize components
                context.Logger.Info("Initializing...");
                foreach (var comp in components)
                {
                    try
                    {
                        comp.Initialize(context);
                    }
                    catch (Exception ex)
                    {
                        context.Logger.ErrorException("Error occured during initialization of '" + comp.Name + "'.", ex);
                        throw new ConfuserException(ex);
                    }
                    context.CheckCancellation();
                }

                // 6. Build pipeline
                context.Logger.Debug("Building pipeline...");
                ProtectionPipeline pipeline = new ProtectionPipeline();
                context.Pipeline = pipeline;
                foreach (var comp in components)
                {
                    comp.PopulatePipeline(pipeline);
                }
                context.CheckCancellation();

                //7. Run pipeline
                RunPipeline(pipeline, context);

                ok = true;
            }
            catch (AssemblyResolveException ex)
            {
                context.Logger.ErrorException("Failed to resolve a assembly, check if all dependencies are of correct version.", ex);
            }
            catch (TypeResolveException ex)
            {
                context.Logger.ErrorException("Failed to resolve a type, check if all dependencies are of correct version.", ex);
            }
            catch (MemberRefResolveException ex)
            {
                context.Logger.ErrorException("Failed to resolve a member, check if all dependencies are of correct version.", ex);
            }
            catch (IOException ex)
            {
                context.Logger.ErrorException("An IO error occured, check if all input/output locations are read/writable.", ex);
            }
            catch (OperationCanceledException)
            {
                context.Logger.Error("Operation is canceled.");
            }
            catch (ConfuserException)
            {
                // Exception is already handled/logged, so just ignore and report failure
            }
            catch (Exception ex)
            {
                context.Logger.ErrorException("Unknown error occured.", ex);
            }
            finally
            {
                context.Logger.Finish(ok);
            }
        }

        /// <summary>
        /// Runs the protection pipeline.
        /// </summary>
        /// <param name="pipeline">The protection pipeline.</param>
        /// <param name="context">The context.</param>
        static void RunPipeline(ProtectionPipeline pipeline, ConfuserContext context)
        {
            Func<IList<IDnlibDef>> getAllDefs = () => context.Modules.SelectMany(module => module.FindDefinitions()).ToList();
            Func<ModuleDef, IList<IDnlibDef>> getModuleDefs = module => module.FindDefinitions().ToList();

            context.CurrentModuleIndex = -1;

            pipeline.ExecuteStage(PipelineStage.Inspection, Inspection, () => getAllDefs(), context);

            ModuleWriterOptionsBase[] options = new ModuleWriterOptionsBase[context.Modules.Count];
            ModuleWriterListener[] listeners = new ModuleWriterListener[context.Modules.Count];
            for (int i = 0; i < context.Modules.Count; i++)
            {
                context.CurrentModuleIndex = i;
                context.CurrentModuleWriterOptions = null;
                context.CurrentModuleWriterListener = null;

                pipeline.ExecuteStage(PipelineStage.BeginModule, BeginModule, () => getModuleDefs(context.CurrentModule), context);
                pipeline.ExecuteStage(PipelineStage.OptimizeMethods, OptimizeMethods, () => getModuleDefs(context.CurrentModule), context);
                pipeline.ExecuteStage(PipelineStage.EndModule, EndModule, () => getModuleDefs(context.CurrentModule), context);

                options[i] = context.CurrentModuleWriterOptions;
                listeners[i] = context.CurrentModuleWriterListener;
            }

            for (int i = 0; i < context.Modules.Count; i++)
            {
                context.CurrentModuleIndex = i;
                context.CurrentModuleWriterOptions = options[i];
                context.CurrentModuleWriterListener = listeners[i];

                pipeline.ExecuteStage(PipelineStage.WriteModule, WriteModule, () => getModuleDefs(context.CurrentModule), context);

                context.OutputModules[i] = context.CurrentModuleOutput;
                context.CurrentModuleWriterOptions = null;
                context.CurrentModuleWriterListener = null;
                context.CurrentModuleOutput = null;
            }

            context.CurrentModuleIndex = -1;

            pipeline.ExecuteStage(PipelineStage.Debug, Debug, () => getAllDefs(), context);
            pipeline.ExecuteStage(PipelineStage.Pack, Pack, () => getAllDefs(), context);
            pipeline.ExecuteStage(PipelineStage.SaveModules, SaveModules, () => getAllDefs(), context);

            context.Logger.Info("Done.");
        }

        static void Inspection(ConfuserContext context)
        {
            context.Logger.Info("Resolving dependencies...");
            foreach (var dependency in context.Modules
                .SelectMany(module => module.GetAssemblyRefs().Select(asmRef => Tuple.Create(asmRef, module))))
            {
                try
                {
                    var assembly = context.Resolver.ResolveThrow(dependency.Item1, dependency.Item2);
                }
                catch (AssemblyResolveException ex)
                {
                    context.Logger.ErrorException("Failed to resolve dependency of '" + dependency.Item2.Name + "'.", ex);
                    throw new ConfuserException(ex);
                }
            }

            context.Logger.Debug("Checking Strong Name...");
            foreach (var module in context.Modules)
            {
                StrongNameKey snKey = context.Annotations.Get<StrongNameKey>(module, Marker.SNKey);
                if (snKey == null && module.IsStrongNameSigned)
                    context.Logger.WarnFormat("[{0}] SN Key is not provided for a signed module, the output may not be working.", module.Name);
                else if (snKey != null && !module.IsStrongNameSigned)
                    context.Logger.WarnFormat("[{0}] SN Key is provided for a unsigned module, the output may not be working.", module.Name);
                else if (snKey != null && module.IsStrongNameSigned &&
                         !module.Assembly.PublicKey.Data.SequenceEqual(snKey.PublicKey))
                    context.Logger.WarnFormat("[{0}] Provided SN Key and signed module's public key do not match, the output may not be working.", module.Name);
            }

            IMarkerService marker = context.Registry.GetService<IMarkerService>();

            context.Logger.Debug("Creating global .cctors...");
            foreach (var module in context.Modules)
            {
                var modType = module.GlobalType;
                if (modType == null)
                {
                    modType = new TypeDefUser("", "<Module>", null);
                    modType.Attributes = TypeAttributes.AnsiClass;
                    module.Types.Add(modType);
                    marker.Mark(modType);
                }
                var cctor = modType.FindOrCreateStaticConstructor();
                if (!marker.IsMarked(cctor))
                    marker.Mark(cctor);
            }

            context.Logger.Debug("Watermarking...");
            foreach (var module in context.Modules)
            {
                var attrRef = module.CorLibTypes.GetTypeRef("System", "Attribute");
                var attrType = new TypeDefUser("", "ConfusedByAttribute", attrRef);
                module.Types.Add(attrType);
                marker.Mark(attrType);

                MethodDefUser ctor = new MethodDefUser(
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
                marker.Mark(ctor);

                CustomAttribute attr = new CustomAttribute(ctor);
                attr.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.String, "ConfuserEx 0.1"));

                module.CustomAttributes.Add(attr);
            }
        }

        static void BeginModule(ConfuserContext context)
        {
            context.Logger.InfoFormat("Processing module '{0}'...", context.CurrentModule.Name);

            context.CurrentModuleWriterListener = new ModuleWriterListener();
            context.CurrentModuleWriterOptions = new ModuleWriterOptions(context.CurrentModule, context.CurrentModuleWriterListener);
            var snKey = context.Annotations.Get<StrongNameKey>(context.CurrentModule, Marker.SNKey);
            context.CurrentModuleWriterOptions.InitializeStrongNameSigning(context.CurrentModule, snKey);

            foreach (var type in context.CurrentModule.GetTypes())
                foreach (var method in type.Methods)
                {
                    if (method.Body != null)
                        method.Body.Instructions.SimplifyMacros(method.Body.Variables, method.Parameters);
                }
        }

        static void OptimizeMethods(ConfuserContext context)
        {
            foreach (var type in context.CurrentModule.GetTypes())
                foreach (var method in type.Methods)
                {
                    if (method.Body != null)
                        method.Body.Instructions.OptimizeMacros();
                }
        }

        static void EndModule(ConfuserContext context)
        {
            string output = context.Modules[context.CurrentModuleIndex].Location;
            if (output != null)
                output = Utils.GetRelativePath(output, context.BaseDirectory);
            else
                output = context.CurrentModule.Name;
            context.OutputPaths[context.CurrentModuleIndex] = output;
        }

        static void WriteModule(ConfuserContext context)
        {
            context.Logger.InfoFormat("Writing module '{0}'...", context.CurrentModule.Name);

            MemoryStream ms = new MemoryStream();
            if (context.CurrentModuleWriterOptions is ModuleWriterOptions)
                context.CurrentModule.Write(ms, (ModuleWriterOptions)context.CurrentModuleWriterOptions);
            else
                context.CurrentModule.NativeWrite(ms, (NativeModuleWriterOptions)context.CurrentModuleWriterOptions);
            context.CurrentModuleOutput = ms.ToArray();
        }

        static void Debug(ConfuserContext context)
        {
            context.Logger.Info("Finalizing...");
            // TODO: Write debug symbols
        }

        static void Pack(ConfuserContext context)
        {
            if (context.Packer != null)
            {
                context.Logger.Info("Packing...");
                context.Packer.Pack(context, new ProtectionParameters(context.Packer, context.Modules.OfType<IDnlibDef>().ToList()));
            }
        }

        static void SaveModules(ConfuserContext context)
        {
            for (int i = 0; i < context.OutputModules.Count; i++)
            {
                string path = Path.GetFullPath(Path.Combine(context.OutputDirectory, context.OutputPaths[i]));
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, context.OutputModules[i]);
            }
        }

        /// <summary>
        /// Prints the copyright stuff and environment information.
        /// </summary>
        /// <param name="context">The working context.</param>
        static void PrintInfo(ConfuserContext context)
        {
            context.Logger.Info("ConfuserEx v0.1 Copyright (C) Ki 2014");

            Type mono = Type.GetType("Mono.Runtime");
            context.Logger.InfoFormat("Running on {0}, {1}, {2} bits",
                Environment.OSVersion,
                mono == null ?
                ".NET Framework v" + Environment.Version :
                mono.GetMethod("GetDisplayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null),
                IntPtr.Size * 8);
        }
    }
}
