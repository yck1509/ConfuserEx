using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Renamer.Analyzers {
	public class WinFormsAnalyzer : IRenamer {
		Dictionary<string, List<PropertyDef>> properties = new Dictionary<string, List<PropertyDef>>();

		public void Analyze(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			if (def is ModuleDef) {
				foreach (var type in ((ModuleDef)def).GetTypes())
					foreach (var prop in type.Properties)
						properties.AddListEntry(prop.Name, prop);
				return;
			}

			var method = def as MethodDef;
			if (method == null || !method.HasBody)
				return;

			AnalyzeMethod(context, service, method);
		}

		void AnalyzeMethod(ConfuserContext context, INameService service, MethodDef method) {
			var binding = new List<Tuple<bool, Instruction>>();
			foreach (Instruction instr in method.Body.Instructions) {
				if ((instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt)) {
					var target = (IMethod)instr.Operand;

					if ((target.DeclaringType.FullName == "System.Windows.Forms.ControlBindingsCollection" ||
					     target.DeclaringType.FullName == "System.Windows.Forms.BindingsCollection") &&
					    target.Name == "Add" && target.MethodSig.Params.Count != 1) {
						binding.Add(Tuple.Create(true, instr));
					}
					else if (target.DeclaringType.FullName == "System.Windows.Forms.Binding" &&
					         target.Name.String == ".ctor") {
						binding.Add(Tuple.Create(false, instr));
					}
				}
			}

			if (binding.Count == 0)
				return;

			var traceSrv = context.Registry.GetService<ITraceService>();
			MethodTrace trace = traceSrv.Trace(method);

			bool erred = false;
			foreach (var instrInfo in binding) {
				int[] args = trace.TraceArguments(instrInfo.Item2);
				if (args == null) {
					if (!erred)
						context.Logger.WarnFormat("Failed to extract binding property name in '{0}'.", method.FullName);
					erred = true;
					continue;
				}

				Instruction propertyName = method.Body.Instructions[args[0 + (instrInfo.Item1 ? 1 : 0)]];
				if (propertyName.OpCode.Code != Code.Ldstr) {
					if (!erred)
						context.Logger.WarnFormat("Failed to extract binding property name in '{0}'.", method.FullName);
					erred = true;
				}
				else {
					List<PropertyDef> props;
					if (!properties.TryGetValue((string)propertyName.Operand, out props)) {
						if (!erred)
							context.Logger.WarnFormat("Failed to extract target property in '{0}'.", method.FullName);
						erred = true;
					}
					else {
						foreach (var property in props)
							service.SetCanRename(property, false);
					}
				}

				Instruction dataMember = method.Body.Instructions[args[2 + (instrInfo.Item1 ? 1 : 0)]];
				if (dataMember.OpCode.Code != Code.Ldstr) {
					if (!erred)
						context.Logger.WarnFormat("Failed to extract binding property name in '{0}'.", method.FullName);
					erred = true;
				}
				else {
					List<PropertyDef> props;
					if (!properties.TryGetValue((string)dataMember.Operand, out props)) {
						if (!erred)
							context.Logger.WarnFormat("Failed to extract target property in '{0}'.", method.FullName);
						erred = true;
					}
					else {
						foreach (var property in props)
							service.SetCanRename(property, false);
					}
				}
			}
		}


		public void PreRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}

		public void PostRename(ConfuserContext context, INameService service, ProtectionParameters parameters, IDnlibDef def) {
			//
		}
	}
}