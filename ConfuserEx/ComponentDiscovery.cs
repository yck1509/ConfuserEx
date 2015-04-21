using System;
using System.Collections.Generic;
using System.Reflection;
using Confuser.Core;

namespace ConfuserEx {
	internal class ComponentDiscovery {
		static void CrossDomainLoadComponents() {
			var ctx = (CrossDomainContext)AppDomain.CurrentDomain.GetData("ctx");
			// Initialize the version resolver callback
			ConfuserEngine.Version.ToString();

			Assembly assembly = Assembly.LoadFile(ctx.PluginPath);
			foreach (var module in assembly.GetLoadedModules())
				foreach (var i in module.GetTypes()) {
					if (i.IsAbstract || !PluginDiscovery.HasAccessibleDefConstructor(i))
						continue;

					if (typeof(Protection).IsAssignableFrom(i)) {
						var prot = (Protection)Activator.CreateInstance(i);
						ctx.AddProtection(Info.FromComponent(prot, ctx.PluginPath));
					}
					else if (typeof(Packer).IsAssignableFrom(i)) {
						var packer = (Packer)Activator.CreateInstance(i);
						ctx.AddPacker(Info.FromComponent(packer, ctx.PluginPath));
					}
				}
		}

		public static void LoadComponents(IList<ConfuserComponent> protections, IList<ConfuserComponent> packers, string pluginPath) {
			var ctx = new CrossDomainContext(protections, packers, pluginPath);
			AppDomain appDomain = AppDomain.CreateDomain("");
			appDomain.SetData("ctx", ctx);
			appDomain.DoCallBack(CrossDomainLoadComponents);
			AppDomain.Unload(appDomain);
		}

		public static void RemoveComponents(IList<ConfuserComponent> protections, IList<ConfuserComponent> packers, string pluginPath) {
			protections.RemoveWhere(comp => comp is InfoComponent && ((InfoComponent)comp).info.path == pluginPath);
			packers.RemoveWhere(comp => comp is InfoComponent && ((InfoComponent)comp).info.path == pluginPath);
		}

		class CrossDomainContext : MarshalByRefObject {
			readonly IList<ConfuserComponent> packers;
			readonly string pluginPath;
			readonly IList<ConfuserComponent> protections;

			public CrossDomainContext(IList<ConfuserComponent> protections, IList<ConfuserComponent> packers, string pluginPath) {
				this.protections = protections;
				this.packers = packers;
				this.pluginPath = pluginPath;
			}

			public string PluginPath {
				get { return pluginPath; }
			}

			public void AddProtection(Info info) {
				foreach (var comp in protections) {
					if (comp.Id == info.id)
						return;
				}
				protections.Add(new InfoComponent(info));
			}

			public void AddPacker(Info info) {
				foreach (var comp in packers) {
					if (comp.Id == info.id)
						return;
				}
				packers.Add(new InfoComponent(info));
			}
		}

		[Serializable]
		class Info {
			public string desc;
			public string fullId;
			public string id;
			public string name;
			public string path;

			public static Info FromComponent(ConfuserComponent component, string pluginPath) {
				var ret = new Info();
				ret.name = component.Name;
				ret.desc = component.Description;
				ret.id = component.Id;
				ret.fullId = component.FullId;
				ret.path = pluginPath;
				return ret;
			}
		}

		class InfoComponent : ConfuserComponent {
			public readonly Info info;

			public InfoComponent(Info info) {
				this.info = info;
			}

			public override string Name {
				get { return info.name; }
			}

			public override string Description {
				get { return info.desc; }
			}

			public override string Id {
				get { return info.id; }
			}

			public override string FullId {
				get { return info.fullId; }
			}

			protected override void Initialize(ConfuserContext context) {
				throw new NotSupportedException();
			}

			protected override void PopulatePipeline(ProtectionPipeline pipeline) {
				throw new NotSupportedException();
			}
		}
	}
}