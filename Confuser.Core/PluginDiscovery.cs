using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Confuser.Core {
	/// <summary>
	///     Discovers available protection plugins.
	/// </summary>
	public class PluginDiscovery {
		/// <summary>
		///     The default plugin discovery service.
		/// </summary>
		internal static readonly PluginDiscovery Instance = new PluginDiscovery();

		/// <summary>
		///     Initializes a new instance of the <see cref="PluginDiscovery" /> class.
		/// </summary>
		protected PluginDiscovery() { }

		/// <summary>
		///     Retrieves the available protection plugins.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="protections">A list of resolved protections.</param>
		/// <param name="packers">A list of resolved packers.</param>
		/// <param name="components">A list of resolved components.</param>
		public void GetPlugins(ConfuserContext context, out IList<Protection> protections, out IList<Packer> packers, out IList<ConfuserComponent> components) {
			protections = new List<Protection>();
			packers = new List<Packer>();
			components = new List<ConfuserComponent>();
			GetPluginsInternal(context, protections, packers, components);
		}

		/// <summary>
		///     Determines whether the specified type has an accessible default constructor.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns><c>true</c> if the specified type has an accessible default constructor; otherwise, <c>false</c>.</returns>
		public static bool HasAccessibleDefConstructor(Type type) {
			ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
			if (ctor == null) return false;
			return ctor.IsPublic;
		}

		/// <summary>
		///     Adds plugins in the assembly to the protection list.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="protections">The working list of protections.</param>
		/// <param name="packers">The working list of packers.</param>
		/// <param name="components">The working list of components.</param>
		/// <param name="asm">The assembly.</param>
		protected static void AddPlugins(
			ConfuserContext context, IList<Protection> protections, IList<Packer> packers,
			IList<ConfuserComponent> components, Assembly asm) {
			foreach(var module in asm.GetLoadedModules())
				foreach (var i in module.GetTypes()) {
					if (i.IsAbstract || !HasAccessibleDefConstructor(i))
						continue;

					if (typeof(Protection).IsAssignableFrom(i)) {
						try {
							protections.Add((Protection)Activator.CreateInstance(i));
						}
						catch (Exception ex) {
							context.Logger.ErrorException("Failed to instantiate protection '" + i.Name + "'.", ex);
						}
					}
					else if (typeof(Packer).IsAssignableFrom(i)) {
						try {
							packers.Add((Packer)Activator.CreateInstance(i));
						}
						catch (Exception ex) {
							context.Logger.ErrorException("Failed to instantiate packer '" + i.Name + "'.", ex);
						}
					}
					else if (typeof(ConfuserComponent).IsAssignableFrom(i)) {
						try {
							components.Add((ConfuserComponent)Activator.CreateInstance(i));
						}
						catch (Exception ex) {
							context.Logger.ErrorException("Failed to instantiate component '" + i.Name + "'.", ex);
						}
					}
				}
			context.CheckCancellation();
		}

		/// <summary>
		///     Retrieves the available protection plugins.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="protections">The working list of protections.</param>
		/// <param name="packers">The working list of packers.</param>
		/// <param name="components">The working list of components.</param>
		protected virtual void GetPluginsInternal(
			ConfuserContext context, IList<Protection> protections,
			IList<Packer> packers, IList<ConfuserComponent> components) {
			try {
				Assembly protAsm = Assembly.Load("Confuser.Protections");
				AddPlugins(context, protections, packers, components, protAsm);
			}
			catch (Exception ex) {
				context.Logger.WarnException("Failed to load built-in protections.", ex);
			}

			try {
				Assembly renameAsm = Assembly.Load("Confuser.Renamer");
				AddPlugins(context, protections, packers, components, renameAsm);
			}
			catch (Exception ex) {
				context.Logger.WarnException("Failed to load renamer.", ex);
			}

			try {
				Assembly renameAsm = Assembly.Load("Confuser.DynCipher");
				AddPlugins(context, protections, packers, components, renameAsm);
			}
			catch (Exception ex) {
				context.Logger.WarnException("Failed to load dynamic cipher library.", ex);
			}

			foreach (string pluginPath in context.Project.PluginPaths) {
				string realPath = Path.Combine(context.BaseDirectory, pluginPath);
				try {
					Assembly plugin = Assembly.LoadFile(realPath);
					AddPlugins(context, protections, packers, components, plugin);
				}
				catch (Exception ex) {
					context.Logger.WarnException("Failed to load plugin '" + pluginPath + "'.", ex);
				}
			}
		}
	}
}