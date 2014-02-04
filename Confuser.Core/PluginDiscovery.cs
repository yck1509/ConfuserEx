using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Confuser.Core
{
    /// <summary>
    /// Discovers available protection plugins.
    /// </summary>
    public class PluginDiscovery
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginDiscovery"/> class.
        /// </summary>
        protected PluginDiscovery() { }

        /// <summary>
        /// The default plugin discovery service.
        /// </summary>
        internal static readonly PluginDiscovery Instance = new PluginDiscovery();

        /// <summary>
        /// Retrieves the available protection plugins.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="protections">A list of resolved protections.</param>
        /// <param name="packers">A list of resolved packers.</param>
        public void GetPlugins(ConfuserContext context, out IList<Protection> protections, out IList<Packer> packers)
        {
            protections = new List<Protection>();
            packers = new List<Packer>();
            GetPluginsInternal(context, protections, packers);
        }

        /// <summary>
        /// Adds plugins in the assembly to the protection list.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="protections">The working list of protections.</param>
        /// <param name="packers">The working list of packers.</param>
        /// <param name="asm">The assembly.</param>
        protected static void AddPlugins(ConfuserContext context, IList<Protection> protections, IList<Packer> packers, Assembly asm)
        {
            foreach (var i in asm.GetTypes())
            {
                if (typeof(Protection).IsAssignableFrom(i))
                {
                    try
                    {
                        protections.Add((Protection)Activator.CreateInstance(i));
                    }
                    catch (Exception ex)
                    {
                        context.Logger.ErrorException("Failed to instantiate protection '" + i.Name + "'.", ex);
                    }
                }
                else if (typeof(Packer).IsAssignableFrom(i))
                {
                    try
                    {
                        packers.Add((Packer)Activator.CreateInstance(i));
                    }
                    catch (Exception ex)
                    {
                        context.Logger.ErrorException("Failed to instantiate packer '" + i.Name + "'.", ex);
                    }
                }
            }
            context.CheckCancellation();
        }

        /// <summary>
        /// Retrieves the available protection plugins.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="protections">The working list of protections.</param>
        /// <param name="packers">The working list of packers.</param>
        protected virtual void GetPluginsInternal(ConfuserContext context, IList<Protection> protections, IList<Packer> packers)
        {
            try
            {
                Assembly protAsm = Assembly.Load("Confuser.Protections");
                AddPlugins(context, protections, packers, protAsm);
            }
            catch (Exception ex)
            {
                context.Logger.WarnException("Failed to load built-in protections.", ex);
            }

            try
            {
                Assembly renameAsm = Assembly.Load("Confuser.Renamer");
                AddPlugins(context, protections, packers, renameAsm);
            }
            catch (Exception ex)
            {
                context.Logger.WarnException("Failed to load renamer.", ex);
            }
        }
    }
}
