using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Confuser.Renamer.BAML {
	internal class BamlBinaryReader : BinaryReader {
		public BamlBinaryReader(Stream stream)
			: base(stream) { }

		public int ReadEncodedInt() {
			return Read7BitEncodedInt();
		}
	}

	internal class BamlBinaryWriter : BinaryWriter {
		public BamlBinaryWriter(Stream stream)
			: base(stream) { }

		public void WriteEncodedInt(int val) {
			Write7BitEncodedInt(val);
		}
	}

	internal class BamlReader {
		public static BamlDocument ReadDocument(Stream str) {
			var ret = new BamlDocument();
			var reader = new BamlBinaryReader(str);
			{
				var rdr = new BinaryReader(str, Encoding.Unicode);
				uint len = rdr.ReadUInt32();
				ret.Signature = new string(rdr.ReadChars((int)(len >> 1)));
				rdr.ReadBytes((int)(((len + 3) & ~3) - len));
			}
			if (ret.Signature != "MSBAML") throw new NotSupportedException();
			ret.ReaderVersion = new BamlDocument.BamlVersion { Major = reader.ReadUInt16(), Minor = reader.ReadUInt16() };
			ret.UpdaterVersion = new BamlDocument.BamlVersion { Major = reader.ReadUInt16(), Minor = reader.ReadUInt16() };
			ret.WriterVersion = new BamlDocument.BamlVersion { Major = reader.ReadUInt16(), Minor = reader.ReadUInt16() };
			if (ret.ReaderVersion.Major != 0 || ret.ReaderVersion.Minor != 0x60 ||
			    ret.UpdaterVersion.Major != 0 || ret.UpdaterVersion.Minor != 0x60 ||
			    ret.WriterVersion.Major != 0 || ret.WriterVersion.Minor != 0x60)
				throw new NotSupportedException();

			var recs = new Dictionary<long, BamlRecord>();
			while (str.Position < str.Length) {
				long pos = str.Position;
				var type = (BamlRecordType)reader.ReadByte();
				BamlRecord rec = null;
				switch (type) {
					case BamlRecordType.AssemblyInfo:
						rec = new AssemblyInfoRecord();
						break;
					case BamlRecordType.AttributeInfo:
						rec = new AttributeInfoRecord();
						break;
					case BamlRecordType.ConstructorParametersStart:
						rec = new ConstructorParametersStartRecord();
						break;
					case BamlRecordType.ConstructorParametersEnd:
						rec = new ConstructorParametersEndRecord();
						break;
					case BamlRecordType.ConstructorParameterType:
						rec = new ConstructorParameterTypeRecord();
						break;
					case BamlRecordType.ConnectionId:
						rec = new ConnectionIdRecord();
						break;
					case BamlRecordType.ContentProperty:
						rec = new ContentPropertyRecord();
						break;
					case BamlRecordType.DefAttribute:
						rec = new DefAttributeRecord();
						break;
					case BamlRecordType.DefAttributeKeyString:
						rec = new DefAttributeKeyStringRecord();
						break;
					case BamlRecordType.DefAttributeKeyType:
						rec = new DefAttributeKeyTypeRecord();
						break;
					case BamlRecordType.DeferableContentStart:
						rec = new DeferableContentStartRecord();
						break;
					case BamlRecordType.DocumentEnd:
						rec = new DocumentEndRecord();
						break;
					case BamlRecordType.DocumentStart:
						rec = new DocumentStartRecord();
						break;
					case BamlRecordType.ElementEnd:
						rec = new ElementEndRecord();
						break;
					case BamlRecordType.ElementStart:
						rec = new ElementStartRecord();
						break;
					case BamlRecordType.KeyElementEnd:
						rec = new KeyElementEndRecord();
						break;
					case BamlRecordType.KeyElementStart:
						rec = new KeyElementStartRecord();
						break;
					case BamlRecordType.LineNumberAndPosition:
						rec = new LineNumberAndPositionRecord();
						break;
					case BamlRecordType.LinePosition:
						rec = new LinePositionRecord();
						break;
					case BamlRecordType.LiteralContent:
						rec = new LiteralContentRecord();
						break;
					case BamlRecordType.NamedElementStart:
						rec = new NamedElementStartRecord();
						break;
					case BamlRecordType.OptimizedStaticResource:
						rec = new OptimizedStaticResourceRecord();
						break;
					case BamlRecordType.PIMapping:
						rec = new PIMappingRecord();
						break;
					case BamlRecordType.PresentationOptionsAttribute:
						rec = new PresentationOptionsAttributeRecord();
						break;
					case BamlRecordType.Property:
						rec = new PropertyRecord();
						break;
					case BamlRecordType.PropertyArrayEnd:
						rec = new PropertyArrayEndRecord();
						break;
					case BamlRecordType.PropertyArrayStart:
						rec = new PropertyArrayStartRecord();
						break;
					case BamlRecordType.PropertyComplexEnd:
						rec = new PropertyComplexEndRecord();
						break;
					case BamlRecordType.PropertyComplexStart:
						rec = new PropertyComplexStartRecord();
						break;
					case BamlRecordType.PropertyCustom:
						rec = new PropertyCustomRecord();
						break;
					case BamlRecordType.PropertyDictionaryEnd:
						rec = new PropertyDictionaryEndRecord();
						break;
					case BamlRecordType.PropertyDictionaryStart:
						rec = new PropertyDictionaryStartRecord();
						break;
					case BamlRecordType.PropertyListEnd:
						rec = new PropertyListEndRecord();
						break;
					case BamlRecordType.PropertyListStart:
						rec = new PropertyListStartRecord();
						break;
					case BamlRecordType.PropertyStringReference:
						rec = new PropertyStringReferenceRecord();
						break;
					case BamlRecordType.PropertyTypeReference:
						rec = new PropertyTypeReferenceRecord();
						break;
					case BamlRecordType.PropertyWithConverter:
						rec = new PropertyWithConverterRecord();
						break;
					case BamlRecordType.PropertyWithExtension:
						rec = new PropertyWithExtensionRecord();
						break;
					case BamlRecordType.PropertyWithStaticResourceId:
						rec = new PropertyWithStaticResourceIdRecord();
						break;
					case BamlRecordType.RoutedEvent:
						rec = new RoutedEventRecord();
						break;
					case BamlRecordType.StaticResourceEnd:
						rec = new StaticResourceEndRecord();
						break;
					case BamlRecordType.StaticResourceId:
						rec = new StaticResourceIdRecord();
						break;
					case BamlRecordType.StaticResourceStart:
						rec = new StaticResourceStartRecord();
						break;
					case BamlRecordType.StringInfo:
						rec = new StringInfoRecord();
						break;
					case BamlRecordType.Text:
						rec = new TextRecord();
						break;
					case BamlRecordType.TextWithConverter:
						rec = new TextWithConverterRecord();
						break;
					case BamlRecordType.TextWithId:
						rec = new TextWithIdRecord();
						break;
					case BamlRecordType.TypeInfo:
						rec = new TypeInfoRecord();
						break;
					case BamlRecordType.TypeSerializerInfo:
						rec = new TypeSerializerInfoRecord();
						break;
					case BamlRecordType.XmlnsProperty:
						rec = new XmlnsPropertyRecord();
						break;
					case BamlRecordType.XmlAttribute:
					case BamlRecordType.ProcessingInstruction:
					case BamlRecordType.LastRecordType:
					case BamlRecordType.EndAttributes:
					case BamlRecordType.DefTag:
					case BamlRecordType.ClrEvent:
					case BamlRecordType.Comment:
					default:
						throw new NotSupportedException();
				}
				rec.Position = pos;

				rec.Read(reader);
				ret.Add(rec);
				recs.Add(pos, rec);
			}
			for (int i = 0; i < ret.Count; i++) {
				var defer = ret[i] as IBamlDeferRecord;
				if (defer != null)
					defer.ReadDefer(ret, i, _ => recs[_]);
			}

			return ret;
		}
	}

	internal class BamlWriter {
		public static void WriteDocument(BamlDocument doc, Stream str) {
			var writer = new BamlBinaryWriter(str);
			{
				var wtr = new BinaryWriter(str, Encoding.Unicode);
				int len = doc.Signature.Length * 2;
				wtr.Write(len);
				wtr.Write(doc.Signature.ToCharArray());
				wtr.Write(new byte[((len + 3) & ~3) - len]);
			}
			writer.Write(doc.ReaderVersion.Major);
			writer.Write(doc.ReaderVersion.Minor);
			writer.Write(doc.UpdaterVersion.Major);
			writer.Write(doc.UpdaterVersion.Minor);
			writer.Write(doc.WriterVersion.Major);
			writer.Write(doc.WriterVersion.Minor);

			var defers = new List<int>();
			for (int i = 0; i < doc.Count; i++) {
				BamlRecord rec = doc[i];
				rec.Position = str.Position;
				writer.Write((byte)rec.Type);
				rec.Write(writer);
				if (rec is IBamlDeferRecord) defers.Add(i);
			}
			foreach (int i in defers)
				(doc[i] as IBamlDeferRecord).WriteDefer(doc, i, writer);
		}
	}
}