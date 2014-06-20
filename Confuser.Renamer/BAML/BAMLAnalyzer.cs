using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Confuser.Core;
using Confuser.Renamer.References;
using dnlib.DotNet;

namespace Confuser.Renamer.BAML {
	internal class BAMLAnalyzer {
		private readonly Dictionary<ushort, AssemblyDef> assemblyRefs = new Dictionary<ushort, AssemblyDef>();
		private readonly Dictionary<ushort, Tuple<IDnlibDef, TypeDef>> attrRefs = new Dictionary<ushort, Tuple<IDnlibDef, TypeDef>>();
		private readonly ConfuserContext context;
		private readonly Dictionary<string, List<EventDef>> events = new Dictionary<string, List<EventDef>>();
		private readonly Dictionary<string, List<PropertyDef>> properties = new Dictionary<string, List<PropertyDef>>();
		private readonly INameService service;
		private readonly Dictionary<ushort, StringInfoRecord> strings = new Dictionary<ushort, StringInfoRecord>();
		private readonly Dictionary<ushort, TypeSig> typeRefs = new Dictionary<ushort, TypeSig>();
		private readonly Dictionary<string, List<Tuple<AssemblyDef, string>>> xmlns = new Dictionary<string, List<Tuple<AssemblyDef, string>>>();
		private string bamlName;
		private ModuleDefMD module;
		private IKnownThings things;

		private KnownThingsv3 thingsv3;
		private KnownThingsv4 thingsv4;
		private XmlNsContext xmlnsCtx;

		public BAMLAnalyzer(ConfuserContext context, INameService service) {
			this.context = context;
			this.service = service;
			PreInit();
		}

		private void PreInit() {
			// WPF will only look for public instance properties/events
			foreach (TypeDef type in context.Modules.SelectMany(m => m.GetTypes())) {
				foreach (PropertyDef property in type.Properties) {
					if (property.IsPublic() && !property.IsStatic())
						properties.AddListEntry(property.Name, property);
				}

				foreach (EventDef evt in type.Events) {
					if (evt.IsPublic() && !evt.IsStatic())
						events.AddListEntry(evt.Name, evt);
				}
			}
		}

		public BamlDocument Analyze(ModuleDefMD module, string bamlName, byte[] data) {
			this.module = module;
			this.bamlName = bamlName;
			if (module.IsClr40) {
				things = thingsv4 ?? (thingsv4 = new KnownThingsv4(context, module));
			}
			else {
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
			BamlElement trueRoot = rootElem.Children.Single();
			var stack = new Stack<BamlElement>();
			stack.Push(rootElem);
			while (stack.Count > 0) {
				BamlElement elem = stack.Pop();
				ProcessBAMLElement(trueRoot, elem);
				foreach (BamlElement child in elem.Children)
					stack.Push(child);
			}

			return document;
		}

		private void PopulateReferences(BamlDocument document) {
			var clrNs = new Dictionary<string, List<Tuple<AssemblyDef, string>>>();

			assemblyRefs.Clear();
			foreach (AssemblyInfoRecord rec in document.OfType<AssemblyInfoRecord>()) {
				AssemblyDef assembly = context.Resolver.ResolveThrow(rec.AssemblyFullName, module);
				assemblyRefs.Add(rec.AssemblyId, assembly);

				if (!context.Modules.Any(m => m.Assembly == assembly))
					continue;

				foreach (CustomAttribute attr in assembly.CustomAttributes.FindAll("System.Windows.Markup.XmlnsDefinitionAttribute")) {
					clrNs.AddListEntry(
						(UTF8String)attr.ConstructorArguments[0].Value,
						Tuple.Create(assembly, (string)(UTF8String)attr.ConstructorArguments[1].Value));
				}
			}

			xmlnsCtx = new XmlNsContext(document, assemblyRefs);

			typeRefs.Clear();
			foreach (TypeInfoRecord rec in document.OfType<TypeInfoRecord>()) {
				AssemblyDef assembly;
				var asmId = (short)(rec.AssemblyId & 0xfff);
				if (asmId == -1)
					assembly = things.FrameworkAssembly;
				else
					assembly = assemblyRefs[(ushort)asmId];

				// WPF uses Assembly.GetType to load it, so if no assembly specified in the TypeSig, it must be in current assembly.
				AssemblyDef assemblyRef = module.Assembly == assembly ?
					                          null : context.Resolver.ResolveThrow(module.GetAssemblyRefs().Single(r => r.FullName == assembly.FullName), module);

				TypeSig typeSig = TypeNameParser.ParseAsTypeSigReflectionThrow(module, rec.TypeFullName, new DummyAssemblyRefFinder(assemblyRef));
				typeRefs[rec.TypeId] = typeSig;

				AddTypeSigReference(typeSig, new BAMLTypeReference(typeSig, rec));
			}

			attrRefs.Clear();
			foreach (AttributeInfoRecord rec in document.OfType<AttributeInfoRecord>()) {
				TypeSig declType;
				if (typeRefs.TryGetValue(rec.OwnerTypeId, out declType)) {
					TypeDef type = declType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
					attrRefs[rec.AttributeId] = AnalyzeAttributeReference(type, rec);
				}
				else {
					Debug.Assert((short)rec.OwnerTypeId < 0);
					TypeDef declTypeDef = things.Types((KnownTypes)(-(short)rec.OwnerTypeId));
					attrRefs[rec.AttributeId] = AnalyzeAttributeReference(declTypeDef, rec);
				}
			}

			strings.Clear();
			foreach (StringInfoRecord rec in document.OfType<StringInfoRecord>()) {
				strings[rec.StringId] = rec;
			}

			foreach (PIMappingRecord rec in document.OfType<PIMappingRecord>()) {
				var asmId = (short)(rec.AssemblyId & 0xfff);
				AssemblyDef assembly;
				if (asmId == -1)
					assembly = things.FrameworkAssembly;
				else
					assembly = assemblyRefs[(ushort)asmId];

				Tuple<AssemblyDef, string> scope = Tuple.Create(assembly, rec.ClrNamespace);
				clrNs.AddListEntry(rec.XmlNamespace, scope);
			}

			xmlns.Clear();
			foreach (XmlnsPropertyRecord rec in document.OfType<XmlnsPropertyRecord>()) {
				List<Tuple<AssemblyDef, string>> clrMap;
				if (clrNs.TryGetValue(rec.XmlNamespace, out clrMap)) {
					xmlns[rec.Prefix] = clrMap;
					foreach (var scope in clrMap)
						xmlnsCtx.AddNsMap(scope, rec.Prefix);
				}
			}
		}

		private TypeDef ResolveType(ushort typeId) {
			if ((short)typeId < 0)
				return things.Types((KnownTypes)(-(short)typeId));
			return typeRefs[typeId].ToBasicTypeDefOrRef().ResolveTypeDefThrow();
		}

		private TypeSig ResolveType(string typeName, out string prefix) {
			List<Tuple<AssemblyDef, string>> clrNs;

			int index = typeName.IndexOf(':');
			if (index == -1) {
				prefix = "";
				if (!xmlns.TryGetValue(prefix, out clrNs))
					return null;
			}
			else {
				prefix = typeName.Substring(0, index);
				if (!xmlns.TryGetValue(prefix, out clrNs))
					return null;

				typeName = typeName.Substring(index + 1);
			}

			foreach (var ns in clrNs) {
				TypeSig sig = TypeNameParser.ParseAsTypeSigReflectionThrow(module, ns.Item2 + "." + typeName, new DummyAssemblyRefFinder(ns.Item1));
				if (sig.ToBasicTypeDefOrRef().ResolveTypeDef() != null)
					return sig;
			}
			return null;
		}

		private Tuple<IDnlibDef, TypeDef> ResolveAttribute(ushort attrId) {
			if ((short)attrId < 0) {
				Tuple<KnownTypes, PropertyDef, TypeDef> info = things.Properties((KnownProperties)(-(short)attrId));
				return Tuple.Create<IDnlibDef, TypeDef>(info.Item2, info.Item3);
			}
			return attrRefs[attrId];
		}

		private void AddTypeSigReference(TypeSig typeSig, INameReference<IDnlibDef> reference) {
			foreach (ITypeDefOrRef type in typeSig.FindTypeRefs()) {
				TypeDef typeDef = type.ResolveTypeDefThrow();
				if (context.Modules.Contains((ModuleDefMD)typeDef.Module)) {
					service.ReduceRenameMode(typeDef, RenameMode.Letters);
					service.AddReference(typeDef, reference);
				}
			}
		}

		private void ProcessBAMLElement(BamlElement root, BamlElement elem) {
			ProcessElementHeader(elem);
			ProcessElementBody(root, elem);
		}

		private void ProcessElementHeader(BamlElement elem) {
			// Resolve type & properties of the element.
			switch (elem.Header.Type) {
				case BamlRecordType.ConstructorParametersStart:
					elem.Type = elem.Parent.Type;
					elem.Attribute = elem.Parent.Attribute;
					break;

				case BamlRecordType.DocumentStart:
					break;

				case BamlRecordType.ElementStart:
				case BamlRecordType.NamedElementStart:
					elem.Type = ResolveType(((ElementStartRecord)elem.Header).TypeId);
					if (elem.Type.FullName == "System.Windows.Data.Binding") {
						// Here comes the trouble...
						// Aww, never mind...
						foreach (BamlElement child in elem.Children) {
							if (child.Header.Type == BamlRecordType.ConstructorParametersStart) {
								var cnt = (TextRecord)child.Body[0];
								string value = cnt.Value;
								if (cnt is TextWithIdRecord)
									value = strings[((TextWithIdRecord)cnt).ValueId].Value;
								AnalyzePropertyPath(cnt.Value);
							}
						}
					}
					else if (elem.Type.FullName == "System.Windows.Markup.TypeExtension") {
						foreach (BamlElement child in elem.Children) {
							if (child.Header.Type == BamlRecordType.ConstructorParametersStart) {
								var cnt = (TextRecord)child.Body[0];
								string value = cnt.Value;
								if (cnt is TextWithIdRecord)
									value = strings[((TextWithIdRecord)cnt).ValueId].Value;

								string prefix;
								TypeSig sig = ResolveType(value.Trim(), out prefix);
								if (sig != null && context.Modules.Contains((ModuleDefMD)sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module)) {
									var reference = new BAMLConverterTypeReference(xmlnsCtx, sig, cnt);
									AddTypeSigReference(sig, reference);
								}
							}
						}
					}
					elem.Attribute = elem.Parent.Attribute;
					if (elem.Attribute != null)
						elem.Type = GetAttributeType(elem.Attribute);
					break;

				case BamlRecordType.PropertyArrayStart:
				case BamlRecordType.PropertyComplexStart:
				case BamlRecordType.PropertyDictionaryStart:
				case BamlRecordType.PropertyListStart:
					Tuple<IDnlibDef, TypeDef> attrInfo = ResolveAttribute(((PropertyComplexStartRecord)elem.Header).AttributeId);
					elem.Type = attrInfo.Item2;
					elem.Attribute = attrInfo.Item1;
					if (elem.Attribute != null)
						elem.Type = GetAttributeType(elem.Attribute);
					break;

				case BamlRecordType.KeyElementStart:
					// i.e. <x:Key></x:Key>
					elem.Type = module.CorLibTypes.Object.TypeDefOrRef.ResolveTypeDef();
					elem.Attribute = null;
					break;

				case BamlRecordType.StaticResourceStart:
					throw new NotSupportedException();
			}
		}

		private TypeDef GetAttributeType(IDnlibDef attr) {
			if (attr is PropertyDef)
				return ((PropertyDef)attr).PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
			if (attr is EventDef)
				return ((EventDef)attr).EventType.ResolveTypeDefThrow();
			throw new UnreachableException();
		}

		private void ProcessElementBody(BamlElement root, BamlElement elem) {
			foreach (BamlRecord rec in elem.Body) {
				// Resolve the type & property for simple property record too.
				TypeDef type = null;
				IDnlibDef attr = null;
				if (rec is PropertyRecord) {
					var propRec = (PropertyRecord)rec;
					Tuple<IDnlibDef, TypeDef> attrInfo = ResolveAttribute(propRec.AttributeId);
					type = attrInfo.Item2;
					attr = attrInfo.Item1;
					if (attr != null)
						type = GetAttributeType(attr);

					if (attrInfo.Item1 is EventDef) {
						MethodDef method = root.Type.FindMethod(propRec.Value);
						if (method == null)
							context.Logger.WarnFormat("Cannot resolve method '{0}' in '{1}'.", root.Type.FullName, propRec.Value);
						else {
							var reference = new BAMLAttributeReference(method, propRec);
							service.AddReference(method, reference);
						}
					}

					if (rec is PropertyWithConverterRecord) {
						ProcessConverter((PropertyWithConverterRecord)rec, type);
					}
				}
				else if (rec is PropertyComplexStartRecord) {
					Tuple<IDnlibDef, TypeDef> attrInfo = ResolveAttribute(((PropertyComplexStartRecord)rec).AttributeId);
					type = attrInfo.Item2;
					attr = attrInfo.Item1;
					if (attr != null)
						type = GetAttributeType(attr);
				}
				else if (rec is ContentPropertyRecord) {
					Tuple<IDnlibDef, TypeDef> attrInfo = ResolveAttribute(((ContentPropertyRecord)rec).AttributeId);
					type = attrInfo.Item2;
					attr = attrInfo.Item1;
					if (elem.Attribute != null && attr != null)
						type = GetAttributeType(attr);
					foreach (BamlElement child in elem.Children) {
						child.Type = type;
						child.Attribute = attr;
					}
				}
				else if (rec is PropertyCustomRecord) {
					var customRec = (PropertyCustomRecord)rec;
					Tuple<IDnlibDef, TypeDef> attrInfo = ResolveAttribute(customRec.AttributeId);
					type = attrInfo.Item2;
					attr = attrInfo.Item1;
					if (elem.Attribute != null && attr != null)
						type = GetAttributeType(attr);

					if ((customRec.SerializerTypeId & 0x4000) != 0 && (customRec.SerializerTypeId & 0x4000) == 0x89) {
						// See BamlRecordReader.GetCustomDependencyPropertyValue.
						// Umm... Well, actually nothing to do, since this record only describe DP, which already won't be renamed.
					}
				}
				else if (rec is PropertyWithExtensionRecord) {
					var extRec = (PropertyWithExtensionRecord)rec;
					Tuple<IDnlibDef, TypeDef> attrInfo = ResolveAttribute(extRec.AttributeId);
					type = attrInfo.Item2;
					attr = attrInfo.Item1;
					if (elem.Attribute != null && attr != null)
						type = GetAttributeType(attr);

					// Umm... Nothing to do here too, since the value only contains either typeId/memberId, which already have references attached.
				}
			}
		}

		private void ProcessConverter(PropertyWithConverterRecord rec, TypeDef type) {
			TypeDef converter = ResolveType(rec.ConverterTypeId);

			if (converter.FullName == "System.ComponentModel.EnumConverter") {
				if (type != null && context.Modules.Contains((ModuleDefMD)type.Module)) {
					FieldDef enumField = type.FindField(rec.Value);
					if (enumField != null)
						service.AddReference(enumField, new BAMLEnumReference(enumField, rec));
				}
			}
			else if (converter.FullName == "System.Windows.Input.CommandConverter") {
				string cmd = rec.Value.Trim();
				int index = cmd.IndexOf('.');
				if (index != -1) {
					string typeName = cmd.Substring(0, index);
					string prefix;
					TypeSig sig = ResolveType(typeName, out prefix);
					if (sig != null) {
						string cmdName = cmd.Substring(index + 1);

						TypeDef typeDef = sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
						if (context.Modules.Contains((ModuleDefMD)typeDef.Module)) {
							PropertyDef property = typeDef.FindProperty(cmdName);
							if (property != null) {
								var reference = new BAMLConverterMemberReference(xmlnsCtx, sig, property, rec);
								AddTypeSigReference(sig, reference);
								service.ReduceRenameMode(property, RenameMode.Letters);
								service.AddReference(property, reference);
							}
							FieldDef field = typeDef.FindField(cmdName);
							if (field != null) {
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
			else if (converter.FullName == "System.Windows.Markup.DependencyPropertyConverter") {
				// Umm... Again nothing to do, DP already won't be renamed.
			}
			else if (converter.FullName == "System.Windows.PropertyPathConverter") {
				AnalyzePropertyPath(rec.Value);
			}
			else if (converter.FullName == "System.Windows.Markup.RoutedEventConverter") {
				throw new NotImplementedException();
			}
			else if (converter.FullName == "System.Windows.Markup.TypeTypeConverter") {
				string prefix;
				TypeSig sig = ResolveType(rec.Value.Trim(), out prefix);
				if (sig != null && context.Modules.Contains((ModuleDefMD)sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module)) {
					var reference = new BAMLConverterTypeReference(xmlnsCtx, sig, rec);
					AddTypeSigReference(sig, reference);
				}
			}
		}

		private Tuple<IDnlibDef, TypeDef> AnalyzeAttributeReference(TypeDef declType, AttributeInfoRecord rec) {
			IDnlibDef retDef = null;
			TypeDef retType = null;
			while (declType != null) {
				PropertyDef property = declType.FindProperty(rec.Name);
				if (property != null) {
					retDef = property;
					retType = property.PropertySig.RetType.ToBasicTypeDefOrRef().ResolveTypeDefThrow();
					if (context.Modules.Contains((ModuleDefMD)declType.Module))
						service.AddReference(property, new BAMLAttributeReference(property, rec));
					break;
				}

				EventDef evt = declType.FindEvent(rec.Name);
				if (evt != null) {
					retDef = evt;
					retType = evt.EventType.ResolveTypeDefThrow();
					if (context.Modules.Contains((ModuleDefMD)declType.Module))
						service.AddReference(evt, new BAMLAttributeReference(evt, rec));
					break;
				}

				if (declType.BaseType == null)
					break;
				declType = declType.BaseType.ResolveTypeDefThrow();
			}
			return Tuple.Create(retDef, retType);
		}

		private void AnalyzePropertyPath(string path) {
			var propertyPath = new PropertyPath(path);
			foreach (PropertyPathPart part in propertyPath.Parts) {
				if (part.IsAttachedDP()) {
					string type, property;
					part.ExtractAttachedDP(out type, out property);
					if (type != null) {
						string prefix;
						TypeSig sig = ResolveType(type, out prefix);
						if (sig != null && context.Modules.Contains((ModuleDefMD)sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module)) {
							var reference = new BAMLPathTypeReference(xmlnsCtx, sig, part);
							AddTypeSigReference(sig, reference);
						}
					}
				}
				else {
					List<PropertyDef> candidates;
					if (properties.TryGetValue(part.Name, out candidates))
						foreach (PropertyDef property in candidates) {
							service.SetCanRename(property, false);
						}
				}

				if (part.IndexerArguments != null) {
					foreach (PropertyPathIndexer indexer in part.IndexerArguments)
						if (!string.IsNullOrEmpty(indexer.Type)) {
							string prefix;
							TypeSig sig = ResolveType(indexer.Type, out prefix);
							if (sig != null && context.Modules.Contains((ModuleDefMD)sig.ToBasicTypeDefOrRef().ResolveTypeDefThrow().Module)) {
								var reference = new BAMLPathTypeReference(xmlnsCtx, sig, part);
								AddTypeSigReference(sig, reference);
							}
						}
				}
			}
		}

		private class DummyAssemblyRefFinder : IAssemblyRefFinder {
			private readonly AssemblyDef assemblyDef;

			public DummyAssemblyRefFinder(AssemblyDef assemblyDef) {
				this.assemblyDef = assemblyDef;
			}

			public AssemblyRef FindAssemblyRef(TypeRef nonNestedTypeRef) {
				return assemblyDef.ToAssemblyRef();
			}
		}

		internal class XmlNsContext {
			private readonly Dictionary<AssemblyDef, ushort> assemblyRefs;
			private readonly BamlDocument doc;
			private readonly Dictionary<Tuple<AssemblyDef, string>, string> xmlNsMap = new Dictionary<Tuple<AssemblyDef, string>, string>();
			private int rootIndex = -1;
			private int x;

			public XmlNsContext(BamlDocument doc, Dictionary<ushort, AssemblyDef> assemblyRefs) {
				this.doc = doc;

				this.assemblyRefs = new Dictionary<AssemblyDef, ushort>();
				foreach (var entry in assemblyRefs)
					this.assemblyRefs[entry.Value] = entry.Key;

				for (int i = 0; i < doc.Count; i++)
					if (doc[i] is ElementStartRecord) {
						rootIndex = i + 1;
						break;
					}
				Debug.Assert(rootIndex != -1);
			}

			public void AddNsMap(Tuple<AssemblyDef, string> scope, string prefix) {
				xmlNsMap[scope] = prefix;
			}

			public string GetPrefix(string clrNs, AssemblyDef assembly) {
				string prefix;
				if (!xmlNsMap.TryGetValue(Tuple.Create(assembly, clrNs), out prefix)) {
					prefix = "_" + x++;
					ushort assemblyId = assemblyRefs[assembly];
					doc.Insert(rootIndex, new XmlnsPropertyRecord {
						AssemblyIds = new[] { assemblyId },
						Prefix = prefix,
						XmlNamespace = "clr-namespace:" + clrNs
					});
					doc.Insert(rootIndex - 1, new PIMappingRecord {
						AssemblyId = assemblyId,
						ClrNamespace = clrNs,
						XmlNamespace = "clr-namespace:" + clrNs
					});
					rootIndex++;
				}
				return prefix;
			}
		}
	}
}