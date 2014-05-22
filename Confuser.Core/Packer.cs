using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using System.IO;
using Confuser.Core.Project;

namespace Confuser.Core
{
    /// <summary>
    /// Base class of Confuser packers.
    /// </summary>
    /// <remarks>
    /// A parameterless constructor must exists in derived classes to enable plugin discovery.
    /// </remarks>
    public abstract class Packer : ConfuserComponent
    {
        /// <summary>
        /// Executes the packer.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="parameters">The parameters of packer.</param>
        protected internal abstract void Pack(ConfuserContext context, ProtectionParameters parameters);

        /// <summary>
        /// Protects the stub using original project settings replace the current output with the protected stub.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="fileName">The result file name.</param>
        /// <param name="module">The stub module.</param>
        /// <param name="snKey">The strong name key.</param>
        /// <param name="prot">The packer protection that applies to the stub.</param>
        protected void ProtectStub(ConfuserContext context, string fileName, byte[] module, StrongNameKey snKey, Protection prot = null)
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string outDir = Path.Combine(tmpDir, Path.GetRandomFileName());
            Directory.CreateDirectory(tmpDir);
            File.WriteAllBytes(Path.Combine(tmpDir, fileName), module);

            ConfuserProject proj = new ConfuserProject();
            proj.Seed = context.Project.Seed;
            foreach (var rule in context.Project.Rules)
                proj.Rules.Add(rule);
            proj.Add(new ProjectModule()
            {
                Path = fileName
            });
            proj.BaseDirectory = tmpDir;
            proj.OutputDirectory = outDir;

            PluginDiscovery discovery = null;
            if (prot != null)
            {
                var rule = new Rule()
                {
                    Preset = ProtectionPreset.None,
                    Inherit = true,
                    Pattern = ".*"
                };
                rule.Add(new SettingItem<Protection>()
                {
                    Id = prot.Id,
                    Action = SettingItemAction.Add
                });
                proj.Rules.Add(rule);
                discovery = new PackerDiscovery(prot);
            }

            ConfuserEngine.Run(new ConfuserParameters()
            {
                Logger = context.Logger,
                PluginDiscovery = discovery,
                Marker = new PackerMarker(snKey),
                Project = proj
            }, context.token).Wait();

            context.OutputModules = new[] { File.ReadAllBytes(Path.Combine(outDir, fileName)) };
            context.OutputPaths = new[] { fileName };
        }
    }

    class PackerMarker : Marker
    {
        StrongNameKey snKey;
        public PackerMarker(StrongNameKey snKey)
        {
            this.snKey = snKey;
        }

        protected internal override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context)
        {
            var result = base.MarkProject(proj, context);
            foreach (var module in result.Modules)
                context.Annotations.Set(module, Marker.SNKey, snKey);
            return result;
        }
    }

    class PackerDiscovery : PluginDiscovery
    {
        Protection prot;
        public PackerDiscovery(Protection prot)
        {
            this.prot = prot;
        }

        protected override void GetPluginsInternal(ConfuserContext context, IList<Protection> protections, IList<Packer> packers, IList<ConfuserComponent> components)
        {
            base.GetPluginsInternal(context, protections, packers, components);
            protections.Add(prot);
        }
    }
}
