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
		protected override void MarkMember(IDnlibDef member, ConfuserContext context) {
			ModuleDef module = ((IMemberRef)member).Module;
			ProtectionParameters.SetParameters(context, member, ProtectionParameters.GetParameters(context, module));
		}

		protected override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context) {
			var modules = new List<Tuple<ProjectModule, ModuleDefMD>>();
			foreach (ProjectModule module in proj) {

				ModuleDefMD modDef = module.Resolve(proj.BaseDirectory, context.Resolver.DefaultModuleContext);
				context.CheckCancellation();

				context.Resolver.AddToCache(modDef);
				modules.Add(Tuple.Create(module, modDef));
			}

			Tuple<Packer, Dictionary<string, string>> packerInfo = null;
			foreach (var module in modules) {
				context.Logger.InfoFormat("Loading '{0}'...", module.Item1.Path);

				MarkModule(proj, context, module.Item2, module == modules[0], ref packerInfo);

				// Packer parameters are stored in modules
				if (packerInfo != null)
					ProtectionParameters.GetParameters(context, module.Item2)[packerInfo.Item1] = packerInfo.Item2;
			}
			return new MarkerResult(modules.Select(module => module.Item2).ToList(), packerInfo == null ? null : packerInfo.Item1);
		}

		private struct ObfuscationAttributeInfo {
			public bool? ApplyToMembers;
			public bool? Exclude;
			public string FeatureName;
			public string FeatureValue;
		}

		private struct ProtectionSettingsInfo {
			public bool ApplyToMember;
			public bool Exclude;
			public string Settings;
		}

		private class ProtectionSettingsStack {
			private Stack<ProtectionSettingsInfo[]> stack;

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

		private static IEnumerable<ObfuscationAttributeInfo> ReadObfuscationAttributes(IHasCustomAttribute item) {
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

		private static IEnumerable<ProtectionSettingsInfo> ProcessAttributes(IEnumerable<ObfuscationAttributeInfo> attrs) {
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

		private void ApplySettings(ConfuserContext context, IDnlibDef def, IEnumerable<ProtectionSettingsInfo> infos) {
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

		private static readonly Regex NSPattern = new Regex("namespace '([^']*)'");

		private void MarkModule(ConfuserProject proj, ConfuserContext context, ModuleDefMD module, bool isMain,
			ref Tuple<Packer, Dictionary<string, string>> packerInfo) {

			var settingAttrs = new List<ObfuscationAttributeInfo>();
			string snKeyPath = null, snKeyPass = null;
			var namespaceAttrs = new Dictionary<string, List<ObfuscationAttributeInfo>>();

			foreach (var attr in ReadObfuscationAttributes(module.Assembly)) {

				if (attr.FeatureName.Equals("generate debug symbol", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'generate debug symbol'.");
					proj.Debug = bool.Parse(attr.FeatureValue);
				}
				if (proj.Debug) {
					module.LoadPdb();
				}

				if (attr.FeatureName.Equals("random seed", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'random seed'.");
					proj.Seed = attr.FeatureValue;
				}

				else if (attr.FeatureName.Equals("strong name key", StringComparison.OrdinalIgnoreCase)) {
					snKeyPath = Path.Combine(proj.BaseDirectory, attr.FeatureValue);
				}

				else if (attr.FeatureName.Equals("strong name key password", StringComparison.OrdinalIgnoreCase)) {
					snKeyPass = attr.FeatureValue;
				}

				else if (attr.FeatureName.Equals("packer", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'packer'.");
					Packer packer;
					Dictionary<string, string> packerParams;
					new ObfAttrParser(packers).ParsePackerString(attr.FeatureValue, out packer, out packerParams);
					packerInfo = Tuple.Create(packer, packerParams);
				}
				else if (attr.FeatureName == "") {
					settingAttrs.Add(attr);
				}
				else {
					var match = NSPattern.Match(attr.FeatureName);
					if (match.Success) {
						string ns = match.Groups[1].Value;
						var x = attr;
						x.FeatureName = "";
						namespaceAttrs.AddListEntry(ns, x);
					}
				}
			}

			ProcessModule(module, context, snKeyPath, snKeyPass, settingAttrs, namespaceAttrs);
		}

		private void ProcessModule(ModuleDefMD module, ConfuserContext context, string snKeyPath, string snKeyPass,
			List<ObfuscationAttributeInfo> settingAttrs,
			Dictionary<string, List<ObfuscationAttributeInfo>> namespaceAttrs) {

			context.Annotations.Set(module, SNKey, LoadSNKey(context, snKeyPath, snKeyPass));

			var moduleStack = new ProtectionSettingsStack();
			moduleStack.Push(ProcessAttributes(settingAttrs));
			ApplySettings(context, module, moduleStack.GetInfos());

			var nsSettings = namespaceAttrs.ToDictionary(kvp => kvp.Key, kvp => {
				var nsStack = new ProtectionSettingsStack(moduleStack);
				nsStack.Push(ProcessAttributes(kvp.Value));
				return nsStack;
			});

			foreach (var type in module.Types) {
				var typeStack = nsSettings.GetValueOrDefault(type.Namespace, moduleStack);
				typeStack.Push(ProcessAttributes(ReadObfuscationAttributes(type)));

				ApplySettings(context, type, typeStack.GetInfos());
				ProcessTypeMembers(type, context, typeStack);

				typeStack.Pop();
			}
		}

		private void ProcessTypeMembers(TypeDef type, ConfuserContext context, ProtectionSettingsStack stack) {
			foreach (var nestedType in type.NestedTypes) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(nestedType)));

				ApplySettings(context, nestedType, stack.GetInfos());
				ProcessTypeMembers(nestedType, context, stack);

				stack.Pop();
			}

			foreach (var prop in type.Properties) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(prop)));

				ApplySettings(context, prop, stack.GetInfos());
				if (prop.GetMethod != null) {
					ProcessMember(prop.GetMethod, context, stack);
				}
				if (prop.SetMethod != null) {
					ProcessMember(prop.GetMethod, context, stack);
				}
				foreach (var m in prop.OtherMethods)
					ProcessMember(m, context, stack);

				stack.Pop();
			}

			foreach (var evt in type.Events) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(evt)));

				ApplySettings(context, evt, stack.GetInfos());
				if (evt.AddMethod != null) {
					ProcessMember(evt.AddMethod, context, stack);
				}
				if (evt.RemoveMethod != null) {
					ProcessMember(evt.RemoveMethod, context, stack);
				}
				if (evt.InvokeMethod != null) {
					ProcessMember(evt.InvokeMethod, context, stack);
				}
				foreach (var m in evt.OtherMethods)
					ProcessMember(m, context, stack);

				stack.Pop();
			}

			foreach (var method in type.Methods) {
				if (method.SemanticsAttributes == 0)
					ProcessMember(method, context, stack);
			}

			foreach (var field in type.Fields) {
				ProcessMember(field, context, stack);
			}
		}

		private void ProcessMember(IDnlibDef member, ConfuserContext context, ProtectionSettingsStack stack) {
			stack.Push(ProcessAttributes(ReadObfuscationAttributes(member)));
			ApplySettings(context, member, stack.GetInfos());
			stack.Pop();
		}
	}
}
