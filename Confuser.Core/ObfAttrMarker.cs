using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
			public IHasCustomAttribute Owner;
			public bool? ApplyToMembers;
			public bool? Exclude;
			public string FeatureName;
			public string FeatureValue;
		}

		struct ProtectionSettingsInfo {
			public bool ApplyToMember;
			public bool Exclude;

			public PatternExpression Condition;
			public string Settings;
		}

		class ProtectionSettingsStack {
			readonly ConfuserContext context;
			readonly Stack<Tuple<ProtectionSettings, ProtectionSettingsInfo[]>> stack;
			readonly ObfAttrParser parser;
			ProtectionSettings settings;

			class PopHolder : IDisposable {
				ProtectionSettingsStack parent;

				public PopHolder(ProtectionSettingsStack parent) {
					this.parent = parent;
				}

				public void Dispose() {
					parent.Pop();
				}
			}

			public ProtectionSettingsStack(ConfuserContext context, Dictionary<string, Protection> protections) {
				this.context = context;
				stack = new Stack<Tuple<ProtectionSettings, ProtectionSettingsInfo[]>>();
				parser = new ObfAttrParser(protections);
			}

			public ProtectionSettingsStack(ProtectionSettingsStack copy) {
				context = copy.context;
				stack = new Stack<Tuple<ProtectionSettings, ProtectionSettingsInfo[]>>(copy.stack);
				parser = copy.parser;
			}

			void Pop() {
				settings = stack.Pop().Item1;
			}

			public IDisposable Apply(IDnlibDef target, IEnumerable<ProtectionSettingsInfo> infos) {
				ProtectionSettings settings;
				if (this.settings == null)
					settings = new ProtectionSettings();
				else
					settings = new ProtectionSettings(this.settings);

				var infoArray = infos.ToArray();

				if (stack.Count > 0) {
					foreach (var i in stack.Skip(1).Reverse())
						ApplyInfo(target, settings, i.Item2.Where(info => info.Condition != null), false);
					ApplyInfo(target, settings, stack.Peek().Item2, false);
				}

				IDisposable result;
				if (infoArray.Length != 0) {
					var originalSettings = this.settings;

					// the settings that would apply to members
					ApplyInfo(target, settings, infoArray, false);
					this.settings = new ProtectionSettings(settings);

					// the settings that would apply to itself
					ApplyInfo(target, settings, infoArray, true);
					stack.Push(Tuple.Create(originalSettings, infoArray));

					result = new PopHolder(this);
				}
				else
					result = null;

				ProtectionParameters.SetParameters(context, target, settings);
				return result;
			}

			void ApplyInfo(IDnlibDef context, ProtectionSettings settings, IEnumerable<ProtectionSettingsInfo> infos, bool current) {
				foreach (var info in infos) {
					if (info.Condition != null && !(bool)info.Condition.Evaluate(context))
						continue;

					if (info.Exclude) {
						if (info.ApplyToMember)
							settings.Clear();
						continue;
					}

					if ((info.ApplyToMember || current || info.Condition != null) && !string.IsNullOrEmpty(info.Settings)) {
						parser.ParseProtectionString(settings, info.Settings);
					}
				}
			}

			public ProtectionSettings GetCurrentSettings() {
				return new ProtectionSettings(settings);
			}
		}

		static readonly Regex OrderPattern = new Regex("^(\\d+)\\. (.+)$");

		static IEnumerable<ObfuscationAttributeInfo> ReadObfuscationAttributes(IHasCustomAttribute item) {
			var ret = new List<Tuple<int?, ObfuscationAttributeInfo>>();
			for (int i = item.CustomAttributes.Count - 1; i >= 0; i--) {
				var ca = item.CustomAttributes[i];
				if (ca.TypeFullName != "System.Reflection.ObfuscationAttribute")
					continue;

				var info = new ObfuscationAttributeInfo();
				int? order = null;

				info.Owner = item;
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

							var match = OrderPattern.Match(feature);
							if (match.Success) {
								var orderStr = match.Groups[1].Value;
								var f = match.Groups[2].Value;
								int o;
								if (!int.TryParse(orderStr, out o))
									throw new NotSupportedException(string.Format("Failed to parse feature '{0}' in {1} ", feature, item));
								order = o;
								feature = f;
							}

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

				ret.Add(Tuple.Create(order, info));
			}
			ret.Reverse();
			return ret.OrderBy(pair => pair.Item1).Select(pair => pair.Item2);
		}

		bool ToInfo(ObfuscationAttributeInfo attr, out ProtectionSettingsInfo info) {
			info = new ProtectionSettingsInfo();

			info.Condition = null;

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
				context.Logger.WarnFormat("Ignoring rule '{0}' in {1}.", info.Settings, attr.Owner);
				return false;
			}

			if (!string.IsNullOrEmpty(attr.FeatureName))
				throw new ArgumentException("Feature name must not be set. Owner=" + attr.Owner);
			if (info.Exclude && (!string.IsNullOrEmpty(attr.FeatureName) || !string.IsNullOrEmpty(attr.FeatureValue))) {
				throw new ArgumentException("Feature property cannot be set when Exclude is true. Owner=" + attr.Owner);
			}
			return true;
		}

		ProtectionSettingsInfo ToInfo(Rule rule, PatternExpression expr) {
			var info = new ProtectionSettingsInfo();

			info.Condition = expr;

			info.Exclude = false;
			info.ApplyToMember = false;

			var settings = new StringBuilder();
			foreach (var item in rule) {
				settings.Append(item.Action == SettingItemAction.Add ? '+' : '-');
				settings.Append(item.Id);
				if (item.Count > 0) {
					settings.Append('(');
					int i = 0;
					foreach (var arg in item) {
						if (i != 0)
							settings.Append(',');
						settings.AppendFormat("{0}='{1}'", arg.Key, arg.Value.Replace("'", "\\'"));
						i++;
					}
					settings.Append(')');
				}
				settings.Append(';');
			}
			info.Settings = settings.ToString();

			return info;
		}

		IEnumerable<ProtectionSettingsInfo> ReadInfos(IHasCustomAttribute item) {
			foreach (var attr in ReadObfuscationAttributes(item)) {
				ProtectionSettingsInfo info;
				if (ToInfo(attr, out info))
					yield return info;
			}
		}

		ConfuserContext context;
		ConfuserProject project;
		Packer packer;
		Dictionary<string, string> packerParams;
		List<byte[]> extModules;

		static readonly object ModuleSettingsKey = new object();

		/// <inheritdoc />
		protected internal override void MarkMember(IDnlibDef member, ConfuserContext context) {
			ModuleDef module = ((IMemberRef)member).Module;
			var settings = context.Annotations.Get<ProtectionSettings>(module, ModuleSettingsKey);
			ProtectionParameters.SetParameters(context, member, new ProtectionSettings(settings));
		}

		/// <inheritdoc />
		protected internal override MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context) {
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

		void AddRule(ObfuscationAttributeInfo attr, List<ProtectionSettingsInfo> infos) {
			Debug.Assert(attr.FeatureName != null);

			var pattern = attr.FeatureName;
			PatternExpression expr;
			try {
				expr = new PatternParser().Parse(pattern);
			}
			catch (Exception ex) {
				throw new Exception("Error when parsing pattern " + pattern + " in ObfuscationAttribute. Owner=" + attr.Owner, ex);
			}

			var info = new ProtectionSettingsInfo();
			info.Condition = expr;

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

			if (!ok)
				context.Logger.WarnFormat("Ignoring rule '{0}' in {1}.", info.Settings, attr.Owner);
			else
				infos.Add(info);
		}

		void MarkModule(ProjectModule projModule, ModuleDefMD module, Rules rules, bool isMain) {
			string snKeyPath = projModule.SNKeyPath, snKeyPass = projModule.SNKeyPassword;
			var stack = new ProtectionSettingsStack(context, protections);

			var layer = new List<ProtectionSettingsInfo>();
			// Add rules
			foreach (var rule in rules)
				layer.Add(ToInfo(rule.Key, rule.Value));

			// Add obfuscation attributes
			foreach (var attr in ReadObfuscationAttributes(module.Assembly)) {
				if (string.IsNullOrEmpty(attr.FeatureName)) {
					ProtectionSettingsInfo info;
					if (ToInfo(attr, out info))
						layer.Add(info);
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
					AddRule(attr, layer);
				}
			}

			if (project.Debug) {
				module.LoadPdb();
			}

			snKeyPath = snKeyPath == null ? null : Path.Combine(project.BaseDirectory, snKeyPath);
			var snKey = LoadSNKey(context, snKeyPath, snKeyPass);
			context.Annotations.Set(module, SNKey, snKey);

			using (stack.Apply(module, layer))
				ProcessModule(module, stack);
		}

		void ProcessModule(ModuleDefMD module, ProtectionSettingsStack stack) {
			context.Annotations.Set(module, ModuleSettingsKey, stack.GetCurrentSettings());
			foreach (var type in module.Types) {
				using (stack.Apply(type, ReadInfos(type)))
					ProcessTypeMembers(type, stack);
			}
		}

		void ProcessTypeMembers(TypeDef type, ProtectionSettingsStack stack) {
			foreach (var nestedType in type.NestedTypes) {
				using (stack.Apply(nestedType, ReadInfos(nestedType)))
					ProcessTypeMembers(nestedType, stack);
			}

			foreach (var property in type.Properties) {
				using (stack.Apply(property, ReadInfos(property))) {
					if (property.GetMethod != null)
						ProcessMember(property.GetMethod, stack);

					if (property.SetMethod != null)
						ProcessMember(property.SetMethod, stack);

					foreach (var m in property.OtherMethods)
						ProcessMember(m, stack);
				}
			}

			foreach (var evt in type.Events) {
				using (stack.Apply(evt, ReadInfos(evt))) {
					if (evt.AddMethod != null)
						ProcessMember(evt.AddMethod, stack);

					if (evt.RemoveMethod != null)
						ProcessMember(evt.RemoveMethod, stack);

					if (evt.InvokeMethod != null)
						ProcessMember(evt.InvokeMethod, stack);

					foreach (var m in evt.OtherMethods)
						ProcessMember(m, stack);
				}
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
			using (stack.Apply(member, ReadInfos(member)))
				ProcessBody(member as MethodDef, stack);
		}

		void ProcessBody(MethodDef method, ProtectionSettingsStack stack) {
			if (method == null || method.Body == null)
				return;

			var declType = method.DeclaringType;
			foreach (var instr in method.Body.Instructions)
				if (instr.Operand is MethodDef) {
					var cgType = ((MethodDef)instr.Operand).DeclaringType;
					if (cgType.DeclaringType == declType && cgType.IsCompilerGenerated()) {
						using (stack.Apply(cgType, ReadInfos(cgType)))
							ProcessTypeMembers(cgType, stack);
					}
				}
		}
	}
}
