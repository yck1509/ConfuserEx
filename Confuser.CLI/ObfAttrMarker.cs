using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Confuser.Core;
using Confuser.Core.Project;
using dnlib.DotNet;

namespace Confuser.CLI {
	internal class ObfAttrMarker : Marker {
		struct ObfuscationAttributeInfo {
			public bool? ApplyToMembers;
			public bool? Exclude;
			public string FeatureName;
			public string FeatureValue;
		}

		struct ProtectionSettingsInfo {
			public bool ApplyToMember;
			public bool Exclude;
			public string Settings;
		}

		class ProtectionSettingsStack {
			Stack<ProtectionSettingsInfo[]> stack;

			public ProtectionSettingsStack() {
				stack = new Stack<ProtectionSettingsInfo[]>();
			}

			public ProtectionSettingsStack(ProtectionSettingsStack copy) {
				stack = new Stack<ProtectionSettingsInfo[]>(copy.stack);
			}

			public void Push(IEnumerable<ProtectionSettingsInfo> infos) {
				stack.Push(infos.ToArray());
			}

			public void Pop() {
				stack.Pop();
			}

			public IEnumerable<ProtectionSettingsInfo> GetInfos() {
				return stack.Reverse().SelectMany(infos => infos);
			}
		}

		static IEnumerable<ObfuscationAttributeInfo> ReadObfuscationAttributes(IHasCustomAttribute item) {
			var ret = new List<ObfuscationAttributeInfo>();
			for (int i = item.CustomAttributes.Count - 1; i >= 0; i--) {
				var ca = item.CustomAttributes[i];
				if (ca.TypeFullName != "System.Reflection.ObfuscationAttribute")
					continue;

				var info = new ObfuscationAttributeInfo();
				bool strip = true;
				foreach (var prop in ca.Properties) {
					switch (prop.Name) {
						case "ApplyToMembers":
							Debug.Assert(prop.Type.ElementType == ElementType.Boolean);
							info.ApplyToMembers = (bool)prop.Value;
							break;

						case "Exclude":
							Debug.Assert(prop.Type.ElementType == ElementType.Boolean);
							info.Exclude = (bool)prop.Value;
							break;

						case "StripAfterObfuscation":
							Debug.Assert(prop.Type.ElementType == ElementType.Boolean);
							strip = (bool)prop.Value;
							break;

						case "Feature":
							Debug.Assert(prop.Type.ElementType == ElementType.String);
							string feature = (UTF8String)prop.Value;
							int sepIndex = feature.IndexOf(':');
							if (sepIndex == -1) {
								info.FeatureName = "";
								info.FeatureValue = feature;
							}
							else {
								info.FeatureName = feature.Substring(0, sepIndex);
								info.FeatureValue = feature.Substring(sepIndex + 1);
							}
							break;

						default:
							throw new NotSupportedException("Unsupported property: " + prop.Name);
					}
				}
				if (strip)
					item.CustomAttributes.RemoveAt(i);

				ret.Add(info);
			}
			ret.Reverse();
			return ret;
		}

		static IEnumerable<ProtectionSettingsInfo> ProcessAttributes(IEnumerable<ObfuscationAttributeInfo> attrs) {
			foreach (var attr in attrs) {
				var info = new ProtectionSettingsInfo();

				info.Exclude = (attr.Exclude ?? true);
				info.ApplyToMember = (attr.ApplyToMembers ?? true);
				info.Settings = attr.FeatureValue;

				if (!string.IsNullOrEmpty(attr.FeatureName))
					throw new ArgumentException("Feature name must not be set.");
				if (info.Exclude && (!string.IsNullOrEmpty(attr.FeatureName) || !string.IsNullOrEmpty(attr.FeatureValue))) {
					throw new ArgumentException("Feature property cannot be set when Exclude is true.");
				}
				yield return info;
			}
		}

		void ApplySettings(IDnlibDef def, IEnumerable<ProtectionSettingsInfo> infos) {
			var settings = new ProtectionSettings();

			ProtectionSettingsInfo? last = null;
			var parser = new ObfAttrParser(protections);
			foreach (var info in infos) {
				if (info.Exclude) {
					if (info.ApplyToMember)
						settings.Clear();
					continue;
				}

				last = info;

				if (info.ApplyToMember) {
					parser.ParseProtectionString(settings, info.Settings);
				}
			}
			if (last != null && !last.Value.ApplyToMember) {
				parser.ParseProtectionString(settings, last.Value.Settings);
			}

			ProtectionParameters.SetParameters(context, def, settings);
		}

		static readonly Regex NSPattern = new Regex("namespace '([^']*)'");
		static readonly Regex NSInModulePattern = new Regex("namespace '([^']*)' in module '([^'])'");

		Dictionary<string, Dictionary<Regex, List<ObfuscationAttributeInfo>>> crossModuleAttrs;
		ConfuserContext context;
		ConfuserProject project;
		Packer packer;
		Dictionary<string, string> packerParams;
		List<byte[]> extModules;

		protected override void MarkMember(IDnlibDef member, ConfuserContext context) {
			ModuleDef module = ((IMemberRef)member).Module;
			ProtectionParameters.SetParameters(context, member, ProtectionParameters.GetParameters(context, module));
		}

		protected override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context) {
			crossModuleAttrs = new Dictionary<string, Dictionary<Regex, List<ObfuscationAttributeInfo>>>();
			this.context = context;
			project = proj;
			extModules = new List<byte[]>();

			var modules = new List<Tuple<ProjectModule, ModuleDefMD>>();
			foreach (ProjectModule module in proj) {
				ModuleDefMD modDef = module.Resolve(proj.BaseDirectory, context.Resolver.DefaultModuleContext);
				context.CheckCancellation();

				context.Resolver.AddToCache(modDef);
				modules.Add(Tuple.Create(module, modDef));
			}
			foreach (var module in modules) {
				context.Logger.InfoFormat("Loading '{0}'...", module.Item1.Path);

				MarkModule(module.Item2, module == modules[0]);

				// Packer parameters are stored in modules
				if (packer != null)
					ProtectionParameters.GetParameters(context, module.Item2)[packer] = packerParams;
			}
			return new MarkerResult(modules.Select(module => module.Item2).ToList(), packer, extModules);
		}

		void MarkModule(ModuleDefMD module, bool isMain) {
			var settingAttrs = new List<ObfuscationAttributeInfo>();
			string snKeyPath = null, snKeyPass = null;
			Dictionary<Regex, List<ObfuscationAttributeInfo>> namespaceAttrs;
			if (!crossModuleAttrs.TryGetValue(module.Name, out namespaceAttrs)) {
				namespaceAttrs = new Dictionary<Regex, List<ObfuscationAttributeInfo>>();
			}

			foreach (var attr in ReadObfuscationAttributes(module.Assembly)) {
				if (attr.FeatureName.Equals("generate debug symbol", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'generate debug symbol'.");
					project.Debug = bool.Parse(attr.FeatureValue);
				}
				if (project.Debug) {
					module.LoadPdb();
				}

				if (attr.FeatureName.Equals("random seed", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'random seed'.");
					project.Seed = attr.FeatureValue;
				}
				else if (attr.FeatureName.Equals("strong name key", StringComparison.OrdinalIgnoreCase)) {
					snKeyPath = Path.Combine(project.BaseDirectory, attr.FeatureValue);
				}
				else if (attr.FeatureName.Equals("strong name key password", StringComparison.OrdinalIgnoreCase)) {
					snKeyPass = attr.FeatureValue;
				}
				else if (attr.FeatureName.Equals("packer", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'packer'.");
					new ObfAttrParser(packers).ParsePackerString(attr.FeatureValue, out packer, out packerParams);
				}
				else if (attr.FeatureName.Equals("external module", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can add external modules.");
					var rawModule = new ProjectModule { Path = attr.FeatureValue }.LoadRaw(project.BaseDirectory);
					extModules.Add(rawModule);
				}
				else if (attr.FeatureName == "") {
					settingAttrs.Add(attr);
				}
				else {
					var match = NSInModulePattern.Match(attr.FeatureName);
					if (match.Success) {
						if (!isMain)
							throw new ArgumentException("Only main module can set cross module obfuscation.");
						var ns = TranslateNamespaceRegex(match.Groups[1].Value);
						string targetModule = match.Groups[2].Value;
						var x = attr;
						x.FeatureName = "";
						Dictionary<Regex, List<ObfuscationAttributeInfo>> targetModuleAttrs;
						if (!crossModuleAttrs.TryGetValue(targetModule, out targetModuleAttrs)) {
							targetModuleAttrs = new Dictionary<Regex, List<ObfuscationAttributeInfo>>();
							crossModuleAttrs[targetModule] = targetModuleAttrs;
						}
						targetModuleAttrs.AddListEntry(ns, x);
					}
					match = NSPattern.Match(attr.FeatureName);
					if (match.Success) {
						var ns = TranslateNamespaceRegex(match.Groups[1].Value);
						var x = attr;
						x.FeatureName = "";
						namespaceAttrs.AddListEntry(ns, x);
					}
				}
			}

			ProcessModule(module, snKeyPath, snKeyPass, settingAttrs, namespaceAttrs);
		}

		static Regex TranslateNamespaceRegex(string ns) {
			if (ns == "*")
				return new Regex(".*");
			if (ns.Length >= 2 && ns[0] == '*' && ns[ns.Length - 1] == '*')
				return new Regex(Regex.Escape(ns.Substring(1, ns.Length - 2)));
			if (ns.Length >= 1 && ns[0] == '*')
				return new Regex(Regex.Escape(ns.Substring(1)) + "$");
			if (ns.Length >= 1 && ns[ns.Length - 1] == '*')
				return new Regex(Regex.Escape("^" + ns.Substring(0, ns.Length - 1)));
			return new Regex(Regex.Escape(ns));
		}

		static ProtectionSettingsStack MatchNamespace(Dictionary<Regex, ProtectionSettingsStack> attrs, string ns) {
			foreach (var nsStack in attrs) {
				if (nsStack.Key.IsMatch(ns))
					return nsStack.Value;
			}
			return null;
		}

		void ProcessModule(ModuleDefMD module, string snKeyPath, string snKeyPass,
		                   List<ObfuscationAttributeInfo> settingAttrs,
		                   Dictionary<Regex, List<ObfuscationAttributeInfo>> namespaceAttrs) {
			context.Annotations.Set(module, SNKey, LoadSNKey(context, snKeyPath, snKeyPass));

			var moduleStack = new ProtectionSettingsStack();
			moduleStack.Push(ProcessAttributes(settingAttrs));
			ApplySettings(module, moduleStack.GetInfos());

			var nsSettings = namespaceAttrs.ToDictionary(kvp => kvp.Key, kvp => {
				var nsStack = new ProtectionSettingsStack(moduleStack);
				nsStack.Push(ProcessAttributes(kvp.Value));
				return nsStack;
			});

			foreach (var type in module.Types) {
				var typeStack = MatchNamespace(nsSettings, type.Namespace) ?? moduleStack;
				typeStack.Push(ProcessAttributes(ReadObfuscationAttributes(type)));

				ApplySettings(type, typeStack.GetInfos());
				ProcessTypeMembers(type, typeStack);

				typeStack.Pop();
			}
		}

		void ProcessTypeMembers(TypeDef type, ProtectionSettingsStack stack) {
			foreach (var nestedType in type.NestedTypes) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(nestedType)));

				ApplySettings(nestedType, stack.GetInfos());
				ProcessTypeMembers(nestedType, stack);

				stack.Pop();
			}

			foreach (var prop in type.Properties) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(prop)));

				ApplySettings(prop, stack.GetInfos());
				if (prop.GetMethod != null) {
					ProcessMember(prop.GetMethod, stack);
				}
				if (prop.SetMethod != null) {
					ProcessMember(prop.SetMethod, stack);
				}
				foreach (var m in prop.OtherMethods)
					ProcessMember(m, stack);

				stack.Pop();
			}

			foreach (var evt in type.Events) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(evt)));

				ApplySettings(evt, stack.GetInfos());
				if (evt.AddMethod != null) {
					ProcessMember(evt.AddMethod, stack);
				}
				if (evt.RemoveMethod != null) {
					ProcessMember(evt.RemoveMethod, stack);
				}
				if (evt.InvokeMethod != null) {
					ProcessMember(evt.InvokeMethod, stack);
				}
				foreach (var m in evt.OtherMethods)
					ProcessMember(m, stack);

				stack.Pop();
			}

			foreach (var method in type.Methods) {
				if (method.SemanticsAttributes == 0)
					ProcessMember(method, stack);
			}

			foreach (var field in type.Fields) {
				ProcessMember(field, stack);
			}
		}

		void ProcessMember(IDnlibDef member, ProtectionSettingsStack stack) {
			stack.Push(ProcessAttributes(ReadObfuscationAttributes(member)));
			ApplySettings(member, stack.GetInfos());
			stack.Pop();
		}
	}
}