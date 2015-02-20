using System;
using System.Collections.Generic;
using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using dnlib.IO;
using dnlib.PE;
using SR = System.Reflection;

namespace Confuser.Core {
	internal class NativeEraser {
		static void Erase(Tuple<uint, uint, byte[]> section, uint offset, uint len) {
			Array.Clear(section.Item3, (int)(offset - section.Item1), (int)len);
		}

		static void Erase(List<Tuple<uint, uint, byte[]>> sections, uint beginOffset, uint size) {
			foreach (var sect in sections)
				if (beginOffset >= sect.Item1 && beginOffset + size < sect.Item2) {
					Erase(sect, beginOffset, size);
					break;
				}
		}

		static void Erase(List<Tuple<uint, uint, byte[]>> sections, IFileSection s) {
			foreach (var sect in sections)
				if ((uint)s.StartOffset >= sect.Item1 && (uint)s.EndOffset < sect.Item2) {
					Erase(sect, (uint)s.StartOffset, (uint)(s.EndOffset - s.StartOffset));
					break;
				}
		}

		static void Erase(List<Tuple<uint, uint, byte[]>> sections, uint methodOffset) {
			foreach (var sect in sections)
				if (methodOffset >= sect.Item1) {
					uint f = sect.Item3[methodOffset - sect.Item1];
					uint size;
					switch ((f & 7)) {
						case 2:
						case 6:
							size = (f >> 2) + 1;
							break;

						case 3:
							f |= (uint)((sect.Item3[methodOffset - sect.Item1 + 1]) << 8);
							size = (f >> 12) * 4;
							uint codeSize = BitConverter.ToUInt32(sect.Item3, (int)(methodOffset - sect.Item1 + 4));
							size += codeSize;
							break;
						default:
							return;
					}
					Erase(sect, methodOffset, size);
				}
		}

		public static void Erase(NativeModuleWriter writer, ModuleDefMD module) {
			if (writer == null || module == null)
				return;

			var sections = new List<Tuple<uint, uint, byte[]>>();
			var s = new MemoryStream();
			foreach (var origSect in writer.OrigSections) {
				var oldChunk = origSect.Chunk;
				var sectHdr = origSect.PESection;

				s.SetLength(0);
				oldChunk.WriteTo(new BinaryWriter(s));
				var buf = s.ToArray();
				var newChunk = new BinaryReaderChunk(MemoryImageStream.Create(buf), oldChunk.GetVirtualSize());
				newChunk.SetOffset(oldChunk.FileOffset, oldChunk.RVA);

				origSect.Chunk = newChunk;

				sections.Add(Tuple.Create(
					sectHdr.PointerToRawData,
					sectHdr.PointerToRawData + sectHdr.SizeOfRawData,
					buf));
			}

			var md = module.MetaData;

			var row = md.TablesStream.MethodTable.Rows;
			for (uint i = 1; i <= row; i++) {
				var method = md.TablesStream.ReadMethodRow(i);
				var codeType = ((MethodImplAttributes)method.ImplFlags & MethodImplAttributes.CodeTypeMask);
				if (codeType == MethodImplAttributes.IL)
					Erase(sections, (uint)md.PEImage.ToFileOffset((RVA)method.RVA));
			}

			var res = md.ImageCor20Header.Resources;
			if (res.Size > 0)
				Erase(sections, (uint)res.StartOffset, res.Size);

			Erase(sections, md.ImageCor20Header);
			Erase(sections, md.MetaDataHeader);
			foreach (var stream in md.AllStreams)
				Erase(sections, stream);
		}
	}
}