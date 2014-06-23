using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.BAML;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.IO;

namespace Confuser.Renamer.Analyzers {
	internal class WPFAnalyzer : IRenamer {
		private static readonly object BAMLKey = new object();

		private static readonly Regex ResourceNamePattern = new Regex("^.*\\.g\\.resources$");
		private BAMLAnalyzer analyzer;

		public void Analyze(ConfuserContext context, INameService service, IDnlibDef def) {
			var method = def as MethodDef;
			if (method != null) {
				if (!method.HasBody)
					return;
				AnalyzeMethod(context, service, method);
			}

			var module = def as ModuleDefMD;
			if (module != null) {
				AnalyzeResources(context, service, module);
			}
		}

		public void PreRename(ConfuserContext context, INameService service, IDnlibDef def) {
			//
		}

		public void PostRename(ConfuserContext context, INameService service, IDnlibDef def) {
			var module = def as ModuleDefMD;
			if (module == null)
				return;

			var wpfResInfo = context.Annotations.Get<Dictionary<string, Dictionary<string, BamlDocument>>>(module, BAMLKey);
			if (wpfResInfo == null)
				return;

			foreach (EmbeddedResource res in module.Resources.OfType<EmbeddedResource>()) {
				Dictionary<string, BamlDocument> resInfo;

				if (!wpfResInfo.TryGetValue(res.Name, out resInfo))
					continue;

				var stream = new MemoryStream();
				var writer = new ResourceWriter(stream);

				res.Data.Position = 0;
				var reader = new ResourceReader(new ImageStream(res.Data));
				IDictionaryEnumerator enumerator = reader.GetEnumerator();
				while (enumerator.MoveNext()) {
					var name = (string)enumerator.Key;
					string typeName;
					byte[] data;
					reader.GetResourceData(name, out typeName, out data);

					BamlDocument document;
					if (resInfo.TryGetValue(name, out document)) {
						var docStream = new MemoryStream();
						docStream.Position = 4;
						BamlWriter.WriteDocument(document, docStream);
						docStream.Position = 0;
						docStream.Write(BitConverter.GetBytes((int)docStream.Length - 4), 0, 4);
						data = docStream.ToArray();
					}

					writer.AddResourceData(name, typeName, data);
				}
				writer.Generate();
				res.Data = MemoryImageStream.Create(stream.ToArray());
			}
		}

		private void AnalyzeMethod(ConfuserContext context, INameService service, MethodDef method) {
			var dpRegInstrs = new List<Tuple<bool, Instruction>>();
			var routedEvtRegInstrs = new List<Instruction>();
			foreach (Instruction instr in method.Body.Instructions) {
				if ((instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt)) {
					var regMethod = (IMethod)instr.Operand;

					if (regMethod.DeclaringType.FullName == "System.Windows.DependencyProperty" &&
					    regMethod.Name.String.StartsWith("Register")) {
						dpRegInstrs.Add(Tuple.Create(regMethod.Name.String.StartsWith("RegisterAttached"), instr));
					} else if (regMethod.DeclaringType.FullName == "System.Windows.EventManager" &&
					           regMethod.Name.String == "RegisterRoutedEvent") {
						routedEvtRegInstrs.Add(instr);
					}
				}
			}

			if (dpRegInstrs.Count == 0)
				return;

			var traceSrv = context.Registry.GetService<ITraceService>();
			MethodTrace trace = traceSrv.Trace(method);

			bool erred = false;
			foreach (var instrInfo in dpRegInstrs) {
				int[] args = trace.TraceArguments(instrInfo.Item2);
				if (args == null) {
					if (!erred)
						context.Logger.WarnFormat("Failed to extract dependency property name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}
				Instruction ldstr = method.Body.Instructions[args[0]];
				if (ldstr.OpCode.Code != Code.Ldstr) {
					if (!erred)
						context.Logger.WarnFormat("Failed to extract dependency property name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}

				var name = (string)ldstr.Operand;
				TypeDef declType = method.DeclaringType;
				bool found = false;
				if (instrInfo.Item1) // Attached DP
				{
					MethodDef accessor;
					if ((accessor = declType.FindMethod("Get" + name)) != null && accessor.IsStatic) {
						service.SetCanRename(accessor, false);
						found = true;
					}
					if ((accessor = declType.FindMethod("Set" + name)) != null && accessor.IsStatic) {
						service.SetCanRename(accessor, false);
						found = true;
					}
				}

				// Normal DP
				// Find CLR property for attached DP as well, because it seems attached DP can be use as normal DP as well.
				PropertyDef property = null;
				if ((property = declType.FindProperty(name)) != null) {
					found = true;
					if (property.GetMethod != null)
						service.SetCanRename(property.GetMethod, false);

					if (property.SetMethod != null)
						service.SetCanRename(property.SetMethod, false);

					if (property.HasOtherMethods) {
						foreach (MethodDef accessor in property.OtherMethods)
							service.SetCanRename(accessor, false);
					}
				}
				if (!found) {
					if (instrInfo.Item1)
						context.Logger.WarnFormat("Failed to find the accessors of attached dependency property '{0}' in type '{1}'.",
						                          name, declType.FullName);
					else
						context.Logger.WarnFormat("Failed to find the CLR property of normal dependency property '{0}' in type '{1}'.",
						                          name, declType.FullName);
				}
			}

			erred = false;
			foreach (Instruction instr in routedEvtRegInstrs) {
				int[] args = trace.TraceArguments(instr);
				if (args == null) {
					if (!erred)
						context.Logger.WarnFormat("Failed to extract routed event name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}
				Instruction ldstr = method.Body.Instructions[args[0]];
				if (ldstr.OpCode.Code != Code.Ldstr) {
					if (!erred)
						context.Logger.WarnFormat("Failed to extract routed event name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}

				var name = (string)ldstr.Operand;
				TypeDef declType = method.DeclaringType;

				EventDef eventDef = null;
				if ((eventDef = declType.FindEvent(name)) == null) {
					context.Logger.WarnFormat("Failed to find the CLR event of routed event '{0}' in type '{1}'.",
					                          name, declType.FullName);
				}
				if (eventDef.AddMethod != null)
					service.SetCanRename(eventDef.AddMethod, false);

				if (eventDef.RemoveMethod != null)
					service.SetCanRename(eventDef.RemoveMethod, false);

				if (eventDef.InvokeMethod != null)
					service.SetCanRename(eventDef.InvokeMethod, false);

				if (eventDef.HasOtherMethods) {
					foreach (MethodDef accessor in eventDef.OtherMethods)
						service.SetCanRename(accessor, false);
				}
			}
		}

		private void AnalyzeResources(ConfuserContext context, INameService service, ModuleDefMD module) {
			if (analyzer == null)
				analyzer = new BAMLAnalyzer(context, service);

			var wpfResInfo = new Dictionary<string, Dictionary<string, BamlDocument>>();

			foreach (EmbeddedResource res in module.Resources.OfType<EmbeddedResource>()) {
				Match match = ResourceNamePattern.Match(res.Name);
				if (!match.Success)
					continue;

				var resInfo = new Dictionary<string, BamlDocument>();

				res.Data.Position = 0;
				var reader = new ResourceReader(new ImageStream(res.Data));
				IDictionaryEnumerator enumerator = reader.GetEnumerator();
				while (enumerator.MoveNext()) {
					var name = (string)enumerator.Key;
					if (!name.EndsWith(".baml"))
						continue;

					string typeName;
					byte[] data;
					reader.GetResourceData(name, out typeName, out data);
					BamlDocument document = analyzer.Analyze(module, name, data);
					resInfo.Add(name, document);
				}

				if (resInfo.Count > 0)
					wpfResInfo.Add(res.Name, resInfo);
			}
			if (wpfResInfo.Count > 0)
				context.Annotations.Set(module, BAMLKey, wpfResInfo);
		}
	}
}