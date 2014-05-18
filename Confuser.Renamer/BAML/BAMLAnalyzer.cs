using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Confuser.Core;
using dnlib.DotNet;
using System.IO;
using Confuser.Renamer.References;

namespace Confuser.Renamer.BAML
{
    class BAMLAnalyzer
    {
        ConfuserContext context;
        INameService service;
        ModuleDefMD module;
        string bamlName;
        public BAMLAnalyzer(ConfuserContext context, INameService service)
        {
            this.context = context;
            this.service = service;
            PreInit();
        }

        Dictionary<string, List<PropertyDef>> properties = new Dictionary<string, List<PropertyDef>>();
        Dictionary<string, List<EventDef>> events = new Dictionary<string, List<EventDef>>();

        void PreInit()
        {
            // WPF will only look for public instance properties/events
            foreach (var type in context.Modules.SelectMany(m => m.GetTypes()))
            {
                foreach (var property in type.Properties)
                {
                    if (property.IsPublic() && !property.IsStatic())
                        properties.AddListEntry(property.Name, property);
                }

                foreach (var evt in type.Events)
                {
                    if (evt.IsPublic() && !evt.IsStatic())
                        events.AddListEntry(evt.Name, evt);
                }
            }
        }

        KnownThingsv3 thingsv3;
        KnownThingsv4 thingsv4;
        IKnownThings things;

        public BamlDocument Analyze(ModuleDefMD module, string bamlName, byte[] data)
        {
            this.module = module;
            this.bamlName = bamlName;
            if (module.IsClr40)
            {
                things = thingsv4 ?? (thingsv4 = new KnownThingsv4(context, module));
            }
            else
            {
                things = thingsv3 ?? (thingsv3 = new KnownThingsv3(context, module));
            }

            Debug.Assert(BitConverter.ToInt32(data, 0) == data.Length - 4);

            BamlDocument document = BamlReader.ReadDocument(new MemoryStream(data, 4, data.Length - 4));

            // Remove debug infos
            document.RemoveWhere(rec => rec is LineNumberAndPositionRecord || rec is LinePositionRecord);

            // Populate references
            PopulateReferences(document);

            // Process elements
            BamlElement rootElem = BamlElement.Read(document);
            Stack<BamlElement> stack = new Stack<BamlElement>();
            stack.Push(rootElem);
            while (stack.Count > 0)
            {
                BamlElement elem = stack.Pop();
                ProcessBAMLElement(elem);
                foreach (var child in elem.Children)
                    stack.Push(child);
            }

            return document;
        }

        Dictionary<ushort, AssemblyDef> assemblyRefs = new Dictionary<ushort, AssemblyDef>();
        Dictionary<ushort, TypeSig> typeRefs = new Dictionary<ushort, TypeSig>();
        Dictionary<ushort, Tuple<PropertyDef, TypeDef>> attrRefs = new Dictionary<ushort, Tuple<PropertyDef, TypeDef>>();
        Dictionary<string, List<Tuple<AssemblyDef, string>>> xmlns = new Dictionary<string, List<Tuple<AssemblyDef, string>>>();
        XmlNsContext xmlnsCtx;

        internal class XmlNsContext
        {
            BamlDocument doc;
            Dictionary<AssemblyDef, ushort> assemblyRefs;
            Dictionary<Tuple<AssemblyDef, string>, string> xmlNsMap = new Dictionary<Tuple<AssemblyDef, string>, string>();
            int x = 0;
            int rootIndex = -1;

            public XmlNsContext(BamlDocument doc, Dictionary<ushort, AssemblyDef> assemblyRefs)
            {
                this.doc = doc;

                this.assemblyRefs = new Dictionary<AssemblyDef, ushort>();
                foreach (var entry in assemblyRefs)
                    this.assemblyRefs[entry.Value] = entry.Key;

                for (int i = 0; i < doc.Count; i++)
                    if (doc[i] is ElementStartRecord)
                    {
                        rootIndex = i;
                        break;
                    }
                Debug.Assert(rootIndex != -1);
            }

            public void AddNsMap(Tuple<AssemblyDef, string> scope, string prefix)
            {
                xmlNsMap[scope] = prefix;
            }

            public string GetPrefix(string clrNs, AssemblyDef assembly)
            {
                string prefix;
                if (!xmlNsMap.TryGetValue(Tuple.Create(assembly, clrNs), out prefix))
                {
                    prefix = "_" + x++;
                    ushort assemblyId = assemblyRefs[assembly];
                    doc.Insert(rootIndex, new PIMappingRecord()
                    {
                        AssemblyId = assemblyId,
                        ClrNamespace = clrNs,
                        XmlNamespace = prefix
                    });
                    doc.Insert(rootIndex, new XmlnsPropertyRecord()
                    {
                        AssemblyIds = new ushort[] { assemblyId },
                        Prefix = prefix,
                        XmlNamespace = prefix
                    });
                }
                return prefix;
            }
        }

        class DummyAssemblyRefFinder : IAssemblyRefFinder
        {
            AssemblyDef assemblyDef;
            public DummyAssemblyRefFinder(AssemblyDef assemblyDef)
            {
                this.assemblyDef = assemblyDef;
            }

            public AssemblyRef FindAssemblyRef(TypeRef nonNestedTypeRef)
            {
                return assemblyDef.ToAssemblyRef();
            }
        }

        void PopulateReferences(BamlDocument document)
        {
            var clrNs = new Dictionary<string, List<Tuple<AssemblyDef, string>>>();

            assemblyRefs.Clear();
            foreach (var rec in document.OfType<AssemblyInfoRecord>())
            {
                var assembly = context.Resolver.ResolveThrow(rec.AssemblyFullName, module);
                assemblyRefs.Add(rec.AssemblyId, assembly);

                if (!context.Modules.Any(m => m.Assembly == assembly))
                    continue;

                foreach (var attr in assembly.CustomAttributes.FindAll("System.Windows.Markup.XmlnsDefinitionAttribute"))
                {
                    clrNs.AddListEntry(
                        (UTF8String)attr.ConstructorArguments[0].Value,
                        Tuple.Create(assembly, (string)(UTF8String)attr.ConstructorArguments[1].Value));
                }
            }

            xmlnsCtx = new XmlNsContext(document, assemblyRefs);

            typeRefs.Clear();
            foreach (var rec in document.OfType<TypeInfoRecord>())
            {
                AssemblyDef assembly;
                short asmId = (short)(rec.AssemblyId & 0xfff);
                if (asmId == -1)
                    assembly = things.FrameworkAssembly;
                else
                    assembly = assemblyRefs[(ushort)asmId];

                // WPF uses Assembly.GetType to load it, so if no assembly specified in the TypeSig, it must be in current assembly.
                var assemblyRef = module.Assembly == assembly ?
                    null : context.Resolver.ResolveThrow(module.GetAssemblyRefs().Single(r => r.FullName == assembly.FullName), module);

                var typeSig = TypeNameParser.ParseAsTypeSigReflectionThrow(module, rec.TypeFullName, new DummyAssemblyRefFinder(assemblyRef));
                typeRefs[rec.TypeId] = typeSig;

                AddTypeSigReference(typeSig, new BAMLTypeReference(typeSig, rec));
            }

            attrRefs.Clear();
            foreach (var rec in document.OfType<AttributeInfoRecord>())
            {
                TypeSig declType;
                if (typeRefs.TryGetValue(rec.OwnerTypeId, out declType))
                {
                    var type = declType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
                    attrRefs[rec.AttributeId] = AnalyzeAttributeReference(type, rec);
                }
                else
                {
                    Debug.Assert((short)rec.OwnerTypeId < 0);
                    TypeDef declTypeDef = things.Types((KnownTypes)(-(short)rec.OwnerTypeId));
                    attrRefs[rec.AttributeId] = AnalyzeAttributeReference(declTypeDef, rec);
                }
            }

            foreach (var rec in document.OfType<PIMappingRecord>())
            {
                short asmId = (short)(rec.AssemblyId & 0xfff);
                AssemblyDef assembly;
                if (asmId == -1)
                    assembly = things.FrameworkAssembly;
                else
                    assembly = assemblyRefs[(ushort)asmId];

                var scope = Tuple.Create(assembly, rec.ClrNamespace);
                clrNs.AddListEntry(rec.XmlNamespace, scope);
            }

            xmlns.Clear();
            foreach (var rec in document.OfType<XmlnsPropertyRecord>())
            {
                List<Tuple<AssemblyDef, string>> clrMap;
                if (clrNs.TryGetValue(rec.XmlNamespace, out clrMap))
                {
                    xmlns[rec.Prefix] = clrMap;
                    foreach (var scope in clrMap)
                        xmlnsCtx.AddNsMap(scope, rec.Prefix);
                }
            }
        }

        TypeDef ResolveType(ushort typeId)
        {
            if ((short)typeId < 0)
                return things.Types((KnownTypes)(-(short)typeId));
            else
                return typeRefs[typeId].ToBasicTypeDefOrRef().ResolveTypeDefThrow();
        }

        TypeSig ResolveType(string typeName, out string prefix)
        {
            List<Tuple<AssemblyDef, string>> clrNs;

            int index = typeName.IndexOf(':');
            if (index == -1)
            {
                prefix = "";
                if (!xmlns.TryGetValue(prefix, out clrNs))
                    return null;
            }
            else
            {
                prefix = typeName.Substring(0, index);
                if (!xmlns.TryGetValue(prefix, out clrNs))
                    return null;

                typeName = typeName.Substring(index + 1);
            }

            foreach (var ns in clrNs)
            {
                TypeSig sig = TypeNameParser.ParseAsTypeSigReflectionThrow(module, ns.Item2 + "." + typeName, new DummyAssemblyRefFinder(ns.Item1));
                if (sig.ToBasicTypeDefOrRef().ResolveTypeDef() != null)
                    return sig;
            }
            return null;
        }

        Tuple<PropertyDef, TypeDef> ResolveAttribute(ushort attrId)
        {
            if ((short)attrId < 0)
            {
                var info = things.Properties((KnownProperties)(-(short)attrId));
                return Tuple.Create<PropertyDef, TypeDef>(info.Item2, info.Item3);
            }
            else
                return attrRefs[attrId];
        }

        void AddTypeSigReference(TypeSig typeSig, INameReference<IDnlibDef> reference)
        {
            foreach (var type in typeSig.FindTypeRefs())
            {
                var typeDef = type.ResolveTypeDefThrow();
                if (context.Modules.Contains((ModuleDefMD)typeDef.Module))
                {
                    service.ReduceRenameMode(typeDef, RenameMode.Letters);
                    service.AddReference(typeDef, reference);
                }
            }
        }

        void ProcessBAMLElement(BamlElement elem)
        {
            ProcessElementHeader(elem);
            ProcessElementBody(elem);
        }

        void ProcessElementHeader(BamlElement elem)
        {
            // Resolve type & properties of the element.
            switch (elem.Header.Type)
            {
                case BamlRecordType.ConstructorParametersStart:
                    elem.Type = elem.Parent.Type;
                    elem.Property = elem.Parent.Property;
                    break;

                case BamlRecordType.DocumentStart:
                    break;

                case BamlRecordType.ElementStart:
                case BamlRecordType.NamedElementStart:
                    elem.Type = ResolveType(((ElementStartRecord)elem.Header).TypeId);
                    if (elem.Type.FullName == "System.Windows.Data.Binding")
                    {
                        // Here comes the trouble...
                        // Aww, never mind...
                        foreach (var child in elem.Children)
                        {
                            if (child.Header.Type == BamlRecordType.ConstructorParametersStart)
                            {
                                TextRecord cnt = (TextRecord)child.Body[0];
                                AnalyzePropertyPath(cnt.Value);
                            }
                        }
                    }
                    elem.Property = elem.Parent.Property;
                    if (elem.Property != null)
                        elem.Type = elem.Property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
                    break;

                case BamlRecordType.PropertyArrayStart:
                case BamlRecordType.PropertyComplexStart:
                case BamlRecordType.PropertyDictionaryStart:
                case BamlRecordType.PropertyListStart:
                    var attrInfo = ResolveAttribute(((PropertyComplexStartRecord)elem.Header).AttributeId);
                    elem.Type = attrInfo.Item2;
                    elem.Property = attrInfo.Item1;
                    if (elem.Property != null)
                        elem.Type = elem.Property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
                    break;

                case BamlRecordType.KeyElementStart:
                    // i.e. <x:Key></x:Key>
                    elem.Type = module.CorLibTypes.Object.TypeDefOrRef.ResolveTypeDef();
                    elem.Property = null;
                    break;

                case BamlRecordType.StaticResourceStart:
                    throw new NotSupportedException();
            }
        }

        void ProcessElementBody(BamlElement elem)
        {
            foreach (var rec in elem.Body)
            {
                // Resolve the type & property for simple property record too.
                TypeDef type = null;
                PropertyDef property = null;
                if (rec is PropertyRecord)
                {
                    PropertyRecord propRec = (PropertyRecord)rec;
                    var attrInfo = ResolveAttribute(propRec.AttributeId);
                    type = attrInfo.Item2;
                    property = attrInfo.Item1;
                    if (property != null)
                        type = property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();

                    if (rec is PropertyWithConverterRecord)
                    {
                        ProcessConverter((PropertyWithConverterRecord)rec, type);
                    }
                }
                else if (rec is PropertyComplexStartRecord)
                {
                    var attrInfo = ResolveAttribute(((PropertyComplexStartRecord)rec).AttributeId);
                    type = attrInfo.Item2;
                    property = attrInfo.Item1;
                    if (property != null)
                        type = property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
                }
                else if (rec is ContentPropertyRecord)
                {
                    var attrInfo = ResolveAttribute(((ContentPropertyRecord)rec).AttributeId);
                    type = attrInfo.Item2;
                    property = attrInfo.Item1;
                    if (elem.Property != null)
                        type = property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
                    foreach (var child in elem.Children)
                    {
                        child.Type = type;
                        child.Property = property;
                    }
                }
                else if (rec is PropertyCustomRecord)
                {
                    PropertyCustomRecord customRec = (PropertyCustomRecord)rec;
                    var attrInfo = ResolveAttribute(customRec.AttributeId);
                    type = attrInfo.Item2;
                    property = attrInfo.Item1;
                    if (elem.Property != null)
                        type = property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();

                    if ((customRec.SerializerTypeId & 0x4000) != 0 && (customRec.SerializerTypeId & 0x4000) == 0x89)
                    {
                        // See BamlRecordReader.GetCustomDependencyPropertyValue.
                        // Umm... Well, actually nothing to do, since this record only describe DP, which already won't be renamed.
                    }
                }
                else if (rec is PropertyWithExtensionRecord)
                {
                    PropertyWithExtensionRecord extRec = (PropertyWithExtensionRecord)rec;
                    var attrInfo = ResolveAttribute(extRec.AttributeId);
                    type = attrInfo.Item2;
                    property = attrInfo.Item1;
                    if (elem.Property != null)
                        type = property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();

                    // Umm... Nothing to do here too, since the value only contains either typeId/memberId, which already have references attached.
                }
            }
        }

        void ProcessConverter(PropertyWithConverterRecord rec, TypeDef type)
        {
            var converter = ResolveType(((PropertyWithConverterRecord)rec).ConverterTypeId);

            if (converter.FullName == "System.ComponentModel.EnumConverter")
            {
                if (type != null && context.Modules.Contains((ModuleDefMD)type.Module))
                {
                    FieldDef enumField = type.FindField(rec.Value);
                    if (enumField != null)
                        service.AddReference(enumField, new BAMLEnumReference(enumField, rec));
                }
            }
            else if (converter.FullName == "System.Windows.Input.CommandConverter")
            {
                string cmd = rec.Value.Trim();
                int index = cmd.IndexOf('.');
                if (index != -1)
                {
                    string typeName = cmd.Substring(0, index);
                    string prefix;
                    TypeSig sig = ResolveType(typeName, out prefix);
                    if (sig != null)
                    {
                        string cmdName = cmd.Substring(index + 1);

                        TypeDef typeDef = sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
                        if (context.Modules.Contains((ModuleDefMD)typeDef.Module))
                        {
                            PropertyDef property = typeDef.FindProperty(cmdName);
                            if (property != null)
                            {
                                var reference = new BAMLConverterMemberReference(xmlnsCtx, sig, property, rec);
                                AddTypeSigReference(sig, reference);
                                service.ReduceRenameMode(property, RenameMode.Letters);
                                service.AddReference(property, reference);
                            }
                            FieldDef field = typeDef.FindField(cmdName);
                            if (field != null)
                            {
                                var reference = new BAMLConverterMemberReference(xmlnsCtx, sig, field, rec);
                                AddTypeSigReference(sig, reference);
                                service.ReduceRenameMode(field, RenameMode.Letters);
                                service.AddReference(field, reference);
                            }
                            if (property == null && field == null)
                                context.Logger.WarnFormat("Could not resolve command '{0}' in '{1}'.", cmd, bamlName);
                        }
                    }
                }
            }
            else if (converter.FullName == "System.Windows.Markup.DependencyPropertyConverter")
            {
                // Umm... Again nothing to do, DP already won't be renamed.
            }
            else if (converter.FullName == "System.Windows.PropertyPathConverter")
            {
                AnalyzePropertyPath(rec.Value);
            }
            else if (converter.FullName == "System.Windows.Markup.RoutedEventConverter")
            {
                throw new NotImplementedException();
            }
            else if (converter.FullName == "System.Windows.Markup.TypeTypeConverter")
            {
                string prefix;
                TypeSig sig = ResolveType(rec.Value.Trim(), out prefix);
                if (sig != null && context.Modules.Contains((ModuleDefMD)sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module))
                {
                    var reference = new BAMLConverterTypeReference(xmlnsCtx, sig, rec);
                    AddTypeSigReference(sig, reference);
                }
            }
        }

        Tuple<PropertyDef, TypeDef> AnalyzeAttributeReference(TypeDef declType, AttributeInfoRecord rec)
        {
            PropertyDef retProp = null;
            TypeDef retType = null;
            while (declType != null)
            {
                PropertyDef property = declType.FindProperty(rec.Name);
                if (property != null)
                {
                    retProp = property;
                    retType = property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
                    if (context.Modules.Contains((ModuleDefMD)declType.Module))
                        service.AddReference(property, new BAMLAttributeReference(property, rec));
                    break;
                }

                EventDef evt = declType.FindEvent(rec.Name);
                if (evt != null)
                {
                    retType = evt.EventType.ResolveTypeDefThrow();
                    if (context.Modules.Contains((ModuleDefMD)declType.Module))
                        service.AddReference(evt, new BAMLAttributeReference(evt, rec));
                    break;
                }

                if (declType.BaseType == null)
                    break;
                else
                    declType = declType.BaseType.ResolveTypeDefThrow();
            }
            return Tuple.Create(retProp, retType);
        }

        void AnalyzePropertyPath(string path)
        {
            PropertyPath propertyPath = new PropertyPath(path);
            foreach (var part in propertyPath.Parts)
            {
                if (part.IsAttachedDP())
                {
                    string type, property;
                    part.ExtractAttachedDP(out type, out property);
                    if (type != null)
                    {
                        string prefix;
                        TypeSig sig = ResolveType(type, out prefix);
                        if (sig != null && context.Modules.Contains((ModuleDefMD)sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module))
                        {
                            var reference = new BAMLPathTypeReference(xmlnsCtx, sig, part);
                            AddTypeSigReference(sig, reference);
                        }
                    }
                }
                else
                {
                    List<PropertyDef> candidates;
                    if (properties.TryGetValue(part.Name, out candidates))
                        foreach (var property in candidates)
                        {
                            service.SetCanRename(property, false);
                        }
                }

                if (part.IndexerArguments != null)
                {
                    foreach (var indexer in part.IndexerArguments)
                        if (!string.IsNullOrEmpty(indexer.Type))
                        {
                            string prefix;
                            TypeSig sig = ResolveType(indexer.Type, out prefix);
                            if (sig != null && context.Modules.Contains((ModuleDefMD)sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module))
                            {
                                var reference = new BAMLPathTypeReference(xmlnsCtx, sig, part);
                                AddTypeSigReference(sig, reference);
                            }
                        }
                }
            }
        }

    }
}
