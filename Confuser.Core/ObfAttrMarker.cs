using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Confuser.Core.Project;
using Confuser.Core.Project.Patterns;
using dnlib.DotNet;

namespace Confuser.Core {
	using Rules = Dictionary<Rule, PatternExpression>;

	/// <summary>
	/// Obfuscation Attribute Marker
	/// </summary>
	public class ObfAttrMarker : Marker {
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
			readonly Stack<ProtectionSettingsInfo[]> stack;

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

		IEnumerable<ProtectionSettingsInfo> ProcessAttributes(IEnumerable<ObfuscationAttributeInfo> attrs) {
			bool hasAttr = false;
			ProtectionSettingsInfo info;

			foreach (var attr in attrs) {
				info = new ProtectionSettingsInfo();

				info.Exclude = (attr.Exclude ?? true);
				info.ApplyToMember = (attr.ApplyToMembers ?? true);
				info.Settings = attr.FeatureValue;

				bool ok = true;
				try {
					new ObfAttrParser(protections).ParseProtectionString(null, info.Settings);
				}
				catch {
					ok = false;
				}

				if (!ok) {
					context.Logger.WarnFormat("Ignoring rule '{0}'.", info.Settings);
					continue;
				}

				if (!string.IsNullOrEmpty(attr.FeatureName))
					throw new ArgumentException("Feature name must not be set.");
				if (info.Exclude && (!string.IsNullOrEmpty(attr.FeatureName) || !string.IsNullOrEmpty(attr.FeatureValue))) {
					throw new ArgumentException("Feature property cannot be set when Exclude is true.");
				}
				yield return info;
				hasAttr = true;
			}

			if (!hasAttr) {
				info = new ProtectionSettingsInfo();

				info.Exclude = false;
				info.ApplyToMember = false;
				info.Settings = "";
				yield return info;
			}
		}

		void ApplySettings(IDnlibDef def, Rules rules, IEnumerable<ProtectionSettingsInfo> infos, ProtectionSettings settings = null) {
			if (settings == null)
				settings = new ProtectionSettings();
			else
				settings = new ProtectionSettings(settings);

			ApplyRules(context, def, rules, settings);
			settings = ProtectionParameters.GetParameters(context, def);

			ProtectionSettingsInfo? last = null;
			var parser = new ObfAttrParser(protections);
			foreach (var info in infos) {
				if (info.Exclude) {
					if (info.ApplyToMember)
						settings.Clear();
					continue;
				}

				last = info;

				if (info.ApplyToMember && !string.IsNullOrEmpty(info.Settings)) {
					parser.ParseProtectionString(settings, info.Settings);
				}
			}
			if (last != null && !last.Value.ApplyToMember &&
			    !string.IsNullOrEmpty(last.Value.Settings)) {
				parser.ParseProtectionString(settings, last.Value.Settings);
			}
		}

		static readonly Regex NSPattern = new Regex("namespace '([^']*)'");
		static readonly Regex NSInModulePattern = new Regex("namespace '([^']*)' in module '([^']*)'");

		Dictionary<string, Dictionary<Regex, List<ObfuscationAttributeInfo>>> crossModuleAttrs;
		ConfuserContext context;
		ConfuserProject project;
		Packer packer;
		Dictionary<string, string> packerParams;
		List<byte[]> extModules;

		/// <inheritdoc />
		protected internal override void MarkMember(IDnlibDef member, ConfuserContext context) {
			ModuleDef module = ((IMemberRef)member).Module;
			ProtectionParameters.SetParameters(context, member, ProtectionParameters.GetParameters(context, module));
		}

		/// <inheritdoc />
		protected internal override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context) {
			crossModuleAttrs = new Dictionary<string, Dictionary<Regex, List<ObfuscationAttributeInfo>>>();
			this.context = context;
			project = proj;
			extModules = new List<byte[]>();

			if (proj.Packer != null) {
				if (!packers.ContainsKey(proj.Packer.Id)) {
					context.Logger.ErrorFormat("Cannot find packer with ID '{0}'.", proj.Packer.Id);
					throw new ConfuserException(null);
				}

				packer = packers[proj.Packer.Id];
				packerParams = new Dictionary<string, string>(proj.Packer, StringComparer.OrdinalIgnoreCase);
			}

			var modules = new List<Tuple<ProjectModule, ModuleDefMD>>();
			foreach (ProjectModule module in proj) {
				if (module.IsExternal) {
					extModules.Add(module.LoadRaw(proj.BaseDirectory));
					continue;
				}

				ModuleDefMD modDef = module.Resolve(proj.BaseDirectory, context.Resolver.DefaultModuleContext);
				context.CheckCancellation();

				context.Resolver.AddToCache(modDef);
				modules.Add(Tuple.Create(module, modDef));
			}
			foreach (var module in modules) {
				context.Logger.InfoFormat("Loading '{0}'...", module.Item1.Path);

				Rules rules = ParseRules(proj, module.Item1, context);
				MarkModule(module.Item1, module.Item2, rules, module == modules[0]);

				context.Annotations.Set(module.Item2, RulesKey, rules);

				// Packer parameters are stored in modules
				if (packer != null)
					ProtectionParameters.GetParameters(context, module.Item2)[packer] = packerParams;
			}

			if (proj.Debug && proj.Packer != null)
				context.Logger.Warn("Generated Debug symbols might not be usable with packers!");

			return new MarkerResult(modules.Select(module => module.Item2).ToList(), packer, extModules);
		}

		class RuleAdaptor : ProtectionSettings, IDictionary<ConfuserComponent, Dictionary<string, string>> {
			Rule rule;
			public RuleAdaptor(string pattern) {
				this.rule = new Rule(pattern, ProtectionPreset.None, true);
			}

			public Rule Rule { get { return rule; } }

			bool IDictionary<ConfuserComponent, Dictionary<string, string>>.ContainsKey(ConfuserComponent key) {
				return true;
			}

			void IDictionary<ConfuserComponent, Dictionary<string, string>>.Add(ConfuserComponent key, Dictionary<string, string> value) {
				var item = new SettingItem<Protection>(key.Id, SettingItemAction.Add);
				foreach (var entry in value)
					item.Add(entry.Key, entry.Value);
				rule.Add(item);
			}

			bool IDictionary<ConfuserComponent, Dictionary<string, string>>.Remove(ConfuserComponent key) {
				var item = new SettingItem<Protection>(key.Id, SettingItemAction.Remove);
				rule.Add(item);
				return true;
			}

			Dictionary<string, string> IDictionary<ConfuserComponent, Dictionary<string, string>>.this[ConfuserComponent key] {
				get { return null; }
				set {
					rule.RemoveWhere(i => i.Id == key.Id);
					var item = new SettingItem<Protection>(key.Id, SettingItemAction.Add);
					foreach (var entry in value)
						item.Add(entry.Key, entry.Value);
					rule.Add(item);
				}
			}
		}

		void AddRule(ObfuscationAttributeInfo attr, Rules rules) {
			Debug.Assert(attr.FeatureName != null && attr.FeatureName.StartsWith("@"));

			var pattern = attr.FeatureName.Substring(1);
			PatternExpression expr;
			try {
				expr = new PatternParser().Parse(pattern);
			}
			catch (Exception ex) {
				throw new Exception("Error when parsing pattern " + pattern + " in ObfuscationAttribute", ex);
			}

			var ruleAdaptor = new RuleAdaptor(pattern);
			try {
				new ObfAttrParser(protections).ParseProtectionString(ruleAdaptor, attr.FeatureValue);
			}
			catch (Exception ex) {
				throw new Exception("Error when parsing rule " + attr.FeatureValue + " in ObfuscationAttribute", ex);
			}

			rules.Add(ruleAdaptor.Rule, expr);
		}

		void MarkModule(ProjectModule projModule, ModuleDefMD module, Rules rules, bool isMain) {
			var settingAttrs = new List<ObfuscationAttributeInfo>();
			string snKeyPath = projModule.SNKeyPath, snKeyPass = projModule.SNKeyPassword;
			Dictionary<Regex, List<ObfuscationAttributeInfo>> namespaceAttrs;
			if (!crossModuleAttrs.TryGetValue(module.Name, out namespaceAttrs)) {
				namespaceAttrs = new Dictionary<Regex, List<ObfuscationAttributeInfo>>();
			}

			foreach (var attr in ReadObfuscationAttributes(module.Assembly)) {
				if (string.IsNullOrEmpty(attr.FeatureName)) {
					settingAttrs.Add(attr);
				}
				else if (attr.FeatureName.Equals("generate debug symbol", StringComparison.OrdinalIgnoreCase)) {
					if (!isMain)
						throw new ArgumentException("Only main module can set 'generate debug symbol'.");
					project.Debug = bool.Parse(attr.FeatureValue);
				}
				else if (attr.FeatureName.Equals("random seed", StringComparison.OrdinalIgnoreCase)) {
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
				else {
					if (attr.FeatureName.StartsWith("@")) {
						AddRule(attr, rules);
						continue;
					}

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
					else {
						match = NSPattern.Match(attr.FeatureName);
						if (match.Success) {
							var ns = TranslateNamespaceRegex(match.Groups[1].Value);
							var x = attr;
							x.FeatureName = "";
							namespaceAttrs.AddListEntry(ns, x);
						}
					}
				}
			}

			if (project.Debug) {
				module.LoadPdb();
			}

			ProcessModule(module, rules, snKeyPath, snKeyPass, settingAttrs, namespaceAttrs);
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
			return new Regex("^" + Regex.Escape(ns) + "$");
		}

		static ProtectionSettingsStack MatchNamespace(Dictionary<Regex, ProtectionSettingsStack> attrs, string ns) {
			foreach (var nsStack in attrs) {
				if (nsStack.Key.IsMatch(ns))
					return nsStack.Value;
			}
			return null;
		}

		void ProcessModule(ModuleDefMD module, Rules rules, string snKeyPath, string snKeyPass,
		                   List<ObfuscationAttributeInfo> settingAttrs,
		                   Dictionary<Regex, List<ObfuscationAttributeInfo>> namespaceAttrs) {
			context.Annotations.Set(module, SNKey, LoadSNKey(context, snKeyPath == null ? null : Path.Combine(project.BaseDirectory, snKeyPath), snKeyPass));

			var moduleStack = new ProtectionSettingsStack();
			moduleStack.Push(ProcessAttributes(settingAttrs));
			ApplySettings(module, rules, moduleStack.GetInfos());

			var nsSettings = namespaceAttrs.ToDictionary(kvp => kvp.Key, kvp => {
				var nsStack = new ProtectionSettingsStack(moduleStack);
				nsStack.Push(ProcessAttributes(kvp.Value));
				return nsStack;
			});

			foreach (var type in module.Types) {
				var typeStack = MatchNamespace(nsSettings, type.Namespace) ?? moduleStack;
				typeStack.Push(ProcessAttributes(ReadObfuscationAttributes(type)));

				ApplySettings(type, rules, typeStack.GetInfos());
				ProcessTypeMembers(type, rules, typeStack);

				typeStack.Pop();
			}
		}

		void ProcessTypeMembers(TypeDef type, Rules rules, ProtectionSettingsStack stack) {
			foreach (var nestedType in type.NestedTypes) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(nestedType)));

				ApplySettings(nestedType, rules, stack.GetInfos());
				ProcessTypeMembers(nestedType, rules, stack);

				stack.Pop();
			}

			foreach (var prop in type.Properties) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(prop)));

				ApplySettings(prop, rules, stack.GetInfos());
				if (prop.GetMethod != null) {
					ProcessMember(prop.GetMethod, rules, stack);
				}
				if (prop.SetMethod != null) {
					ProcessMember(prop.SetMethod, rules, stack);
				}
				foreach (var m in prop.OtherMethods)
					ProcessMember(m, rules, stack);

				stack.Pop();
			}

			foreach (var evt in type.Events) {
				stack.Push(ProcessAttributes(ReadObfuscationAttributes(evt)));

				ApplySettings(evt, rules, stack.GetInfos());
				if (evt.AddMethod != null) {
					ProcessMember(evt.AddMethod, rules, stack);
				}
				if (evt.RemoveMethod != null) {
					ProcessMember(evt.RemoveMethod, rules, stack);
				}
				if (evt.InvokeMethod != null) {
					ProcessMember(evt.InvokeMethod, rules, stack);
				}
				foreach (var m in evt.OtherMethods)
					ProcessMember(m, rules, stack);

				stack.Pop();
			}

			foreach (var method in type.Methods) {
				if (method.SemanticsAttributes == 0)
					ProcessMember(method, rules, stack);
			}

			foreach (var field in type.Fields) {
				ProcessMember(field, rules, stack);
			}
		}

		void ProcessMember(IDnlibDef member, Rules rules, ProtectionSettingsStack stack) {
			stack.Push(ProcessAttributes(ReadObfuscationAttributes(member)));
			ApplySettings(member, rules, stack.GetInfos());
			ProcessBody(member as MethodDef, rules, stack);
			stack.Pop();
		}

		void ProcessBody(MethodDef method, Rules rules, ProtectionSettingsStack stack) {
			if (method == null || method.Body == null)
				return;

			var declType = method.DeclaringType;
			foreach (var instr in method.Body.Instructions)
				if (instr.Operand is MethodDef) {
					var cgType = ((MethodDef)instr.Operand).DeclaringType;
					if (cgType.DeclaringType == declType && cgType.IsCompilerGenerated()) {
						ApplySettings(cgType, rules, stack.GetInfos());
						ProcessTypeMembers(cgType, rules, stack);
					}
				}
		}
	}
}
