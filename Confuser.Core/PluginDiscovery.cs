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
        /// <param name="logger">The logger.</param>
        /// <returns>A list of resolved <see cref="Protection" />s.</returns>
        public IList<Protection> GetPlugins(ILogger logger)
        {
            List<Protection> ret = new List<Protection>();
            GetPluginsInternal(logger, ret);
            return ret;
        }

        /// <summary>
        /// Adds plugins in the assembly to the protection list.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="protections">The protections.</param>
        /// <param name="asm">The assembly.</param>
        protected static void AddPlugins(ILogger logger, IList<Protection> protections, Assembly asm)
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
                        logger.ErrorException("Failed to instantiate protection '" + i.Name + "'.", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the available protection plugins.
        /// </summary>
        /// <param name="protections">The working list of protections.</param>
        protected virtual void GetPluginsInternal(ILogger logger, IList<Protection> protections)
        {
            try
            {
                Assembly protAsm = Assembly.Load("Confuser.Protections");
                AddPlugins(logger, protections, protAsm);
            }
            catch (Exception ex)
            {
                logger.WarnException("Failed to load built-in protections.", ex);
            }

            try
            {
                Assembly renameAsm = Assembly.Load("Confuser.Renamer");
                AddPlugins(logger, protections, renameAsm);
            }
            catch (Exception ex)
            {
                logger.WarnException("Failed to load renamer.", ex);
            }
        }
    }
}
