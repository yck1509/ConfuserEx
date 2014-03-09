using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Core.Services;
using dnlib.DotNet.Emit;
using System.Text.RegularExpressions;
using System.Resources;
using System.IO;
using System.Diagnostics;
using Confuser.Renamer.BAML;
using Confuser.Renamer.References;
using dnlib.IO;

namespace Confuser.Renamer.Analyzers
{
    class WPFAnalyzer : IRenamer
    {
        public void Analyze(ConfuserContext context, INameService service, IDefinition def)
        {
            MethodDef method = def as MethodDef;
            if (method != null)
            {
                if (!method.HasBody)
                    return;
                AnalyzeMethod(context, service, method);
            }

            ModuleDefMD module = def as ModuleDefMD;
            if (module != null)
            {
                AnalyzeResources(context, service, module);
            }
        }

        void AnalyzeMethod(ConfuserContext context, INameService service, MethodDef method)
        {
            List<Tuple<bool, Instruction>> dpRegInstrs = new List<Tuple<bool, Instruction>>();
            List<Instruction> routedEvtRegInstrs = new List<Instruction>();
            foreach (var instr in method.Body.Instructions)
            {
                if ((instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt))
                {
                    IMethod regMethod = (IMethod)instr.Operand;

                    if (regMethod.DeclaringType.FullName == "System.Windows.DependencyProperty" &&
                        regMethod.Name.String.StartsWith("Register"))
                    {
                        dpRegInstrs.Add(Tuple.Create(regMethod.Name.String.StartsWith("RegisterAttached"), instr));
                    }

                    else if (regMethod.DeclaringType.FullName == "System.Windows.EventManager" &&
                            regMethod.Name.String == "RegisterRoutedEvent")
                    {
                        routedEvtRegInstrs.Add(instr);
                    }
                }
            }

            if (dpRegInstrs.Count == 0)
                return;

            ITraceService traceSrv = context.Registry.GetService<ITraceService>();
            var trace = traceSrv.Trace(method);

            bool erred = false;
            foreach (var instrInfo in dpRegInstrs)
            {
                int[] args = trace.TraceArguments(instrInfo.Item2);
                if (args == null)
                {
                    if (!erred)
                        context.Logger.WarnFormat("Failed to extract dependency property name in '{0}'.", method.FullName);
                    erred = true;
                    continue;
                }
                Instruction ldstr = method.Body.Instructions[args[0]];
                if (ldstr.OpCode.Code != Code.Ldstr)
                {
                    if (!erred)
                        context.Logger.WarnFormat("Failed to extract dependency property name in '{0}'.", method.FullName);
                    erred = true;
                    continue;
                }

                string name = (string)ldstr.Operand;
                TypeDef declType = method.DeclaringType;
                bool found = false;
                if (instrInfo.Item1) // Attached DP
                {
                    MethodDef accessor;
                    if ((accessor = declType.FindMethod("Get" + name)) != null && accessor.IsStatic)
                    {
                        service.SetCanRename(accessor, false);
                        found = true;
                    }
                    if ((accessor = declType.FindMethod("Set" + name)) != null && accessor.IsStatic)
                    {
                        service.SetCanRename(accessor, false);
                        found = true;
                    }
                }

                // Normal DP
                // Find CLR property for attached DP as well, because it seems attached DP can be use as normal DP as well.
                PropertyDef property = null;
                if ((property = declType.FindProperty(name)) != null)
                {
                    found = true;
                    if (property.GetMethod != null)
                        service.SetCanRename(property.GetMethod, false);

                    if (property.SetMethod != null)
                        service.SetCanRename(property.SetMethod, false);

                    if (property.HasOtherMethods)
                    {
                        foreach (var accessor in property.OtherMethods)
                            service.SetCanRename(accessor, false);
                    }
                }
                if (!found)
                {
                    if (instrInfo.Item1)
                        context.Logger.WarnFormat("Failed to find the accessors of attached dependency property '{0}' in type '{1}'.",
                                                  name, declType.FullName);
                    else
                        context.Logger.WarnFormat("Failed to find the CLR property of normal dependency property '{0}' in type '{1}'.",
                                                  name, declType.FullName);
                }
            }

            erred = false;
            foreach (var instr in routedEvtRegInstrs)
            {
                int[] args = trace.TraceArguments(instr);
                if (args == null)
                {
                    if (!erred)
                        context.Logger.WarnFormat("Failed to extract routed event name in '{0}'.", method.FullName);
                    erred = true;
                    continue;
                }
                Instruction ldstr = method.Body.Instructions[args[0]];
                if (ldstr.OpCode.Code != Code.Ldstr)
                {
                    if (!erred)
                        context.Logger.WarnFormat("Failed to extract routed event name in '{0}'.", method.FullName);
                    erred = true;
                    continue;
                }

                string name = (string)ldstr.Operand;
                TypeDef declType = method.DeclaringType;

                EventDef eventDef = null;
                if ((eventDef = declType.FindEvent(name)) == null)
                {
                    context.Logger.WarnFormat("Failed to find the CLR event of routed event '{0}' in type '{1}'.",
                                              name, declType.FullName);
                }
                if (eventDef.AddMethod != null)
                    service.SetCanRename(eventDef.AddMethod, false);

                if (eventDef.RemoveMethod != null)
                    service.SetCanRename(eventDef.RemoveMethod, false);

                if (eventDef.InvokeMethod != null)
                    service.SetCanRename(eventDef.InvokeMethod, false);

                if (eventDef.HasOtherMethods)
                {
                    foreach (var accessor in eventDef.OtherMethods)
                        service.SetCanRename(accessor, false);
                }
            }
        }

        static readonly object BAMLKey = new object();

        BAMLAnalyzer analyzer;
        static readonly Regex ResourceNamePattern = new Regex("^.*\\.g\\.resources$");
        void AnalyzeResources(ConfuserContext context, INameService service, ModuleDefMD module)
        {
            if (analyzer == null)
                analyzer = new BAMLAnalyzer(context, service);

            Dictionary<string, Dictionary<string, BamlDocument>> wpfResInfo = new Dictionary<string, Dictionary<string, BamlDocument>>();

            foreach (var res in module.Resources.OfType<EmbeddedResource>())
            {
                Match match = ResourceNamePattern.Match(res.Name);
                if (!match.Success)
                    continue;

                Dictionary<string, BamlDocument> resInfo = new Dictionary<string, BamlDocument>();

                res.Data.Position = 0;
                ResourceReader reader = new ResourceReader(new ImageStream(res.Data));
                var enumerator = reader.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    string name = (string)enumerator.Key;
                    if (!name.EndsWith(".baml"))
                        continue;

                    string typeName;
                    byte[] data;
                    reader.GetResourceData(name, out typeName, out data);
                    var document = analyzer.Analyze(module, name, data);
                    resInfo.Add(name, document);
                }

                if (resInfo.Count > 0)
                    wpfResInfo.Add(res.Name, resInfo);
            }
            if (wpfResInfo.Count > 0)
                context.Annotations.Set(module, BAMLKey, wpfResInfo);
        }

        public void PreRename(ConfuserContext context, INameService service, IDefinition def)
        {
            //
        }

        public void PostRename(ConfuserContext context, INameService service, IDefinition def)
        {
            ModuleDefMD module = def as ModuleDefMD;
            if (module == null)
                return;

            var wpfResInfo = context.Annotations.Get<Dictionary<string, Dictionary<string, BamlDocument>>>(module, BAMLKey);
            if (wpfResInfo == null)
                return;

            foreach (var res in module.Resources.OfType<EmbeddedResource>())
            {
                Dictionary<string, BamlDocument> resInfo;

                if (!wpfResInfo.TryGetValue(res.Name, out resInfo))
                    continue;

                MemoryStream stream = new MemoryStream();
                ResourceWriter writer = new ResourceWriter(stream);

                res.Data.Position = 0;
                ResourceReader reader = new ResourceReader(new ImageStream(res.Data));
                var enumerator = reader.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    string name = (string)enumerator.Key;
                    string typeName;
                    byte[] data;
                    reader.GetResourceData(name, out typeName, out data);

                    BamlDocument document;
                    if (resInfo.TryGetValue(name, out document))
                    {
                        MemoryStream docStream = new MemoryStream();
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
    }
}
