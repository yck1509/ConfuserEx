using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using MethodBody = dnlib.DotNet.Writer.MethodBody;

namespace Confuser.Protections.AntiTamper {
	internal class NormalMode : IModeHandler {
		uint c;
		IKeyDeriver deriver;

		List<MethodDef> methods;
		uint name1, name2;
		RandomGenerator random;
		uint v;
		uint x;
		uint z;

		public void HandleInject(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters) {
			random = context.Registry.GetService<IRandomService>().GetRandomGenerator(parent.FullId);
			z = random.NextUInt32();
			x = random.NextUInt32();
			c = random.NextUInt32();
			v = random.NextUInt32();
			name1 = random.NextUInt32() & 0x7f7f7f7f;
			name2 = random.NextUInt32() & 0x7f7f7f7f;

			switch (parameters.GetParameter(context, context.CurrentModule, "key", Mode.Normal)) {
				case Mode.Normal:
					deriver = new NormalDeriver();
					break;
				case Mode.Dynamic:
					deriver = new DynamicDeriver();
					break;
				default:
					throw new UnreachableException();
			}
			deriver.Init(context, random);

			var rt = context.Registry.GetService<IRuntimeService>();
			TypeDef initType = rt.GetRuntimeType("Confuser.Runtime.AntiTamperNormal");
			IEnumerable<IDnlibDef> members = InjectHelper.Inject(initType, context.CurrentModule.GlobalType, context.CurrentModule);
			var initMethod = (MethodDef)members.Single(m => m.Name == "Initialize");

			initMethod.Body.SimplifyMacros(initMethod.Parameters);
			List<Instruction> instrs = initMethod.Body.Instructions.ToList();
			for (int i = 0; i < instrs.Count; i++) {
				Instruction instr = instrs[i];
				if (instr.OpCode == OpCodes.Ldtoken) {
					instr.Operand = context.CurrentModule.GlobalType;
				}
				else if (instr.OpCode == OpCodes.Call) {
					var method = (IMethod)instr.Operand;
					if (method.DeclaringType.Name == "Mutation" &&
					    method.Name == "Crypt") {
						Instruction ldDst = instrs[i - 2];
						Instruction ldSrc = instrs[i - 1];
						Debug.Assert(ldDst.OpCode == OpCodes.Ldloc && ldSrc.OpCode == OpCodes.Ldloc);
						instrs.RemoveAt(i);
						instrs.RemoveAt(i - 1);
						instrs.RemoveAt(i - 2);
						instrs.InsertRange(i - 2, deriver.EmitDerivation(initMethod, context, (Local)ldDst.Operand, (Local)ldSrc.Operand));
					}
				}
			}
			initMethod.Body.Instructions.Clear();
			foreach (Instruction instr in instrs)
				initMethod.Body.Instructions.Add(instr);

			MutationHelper.InjectKeys(initMethod,
			                          new[] { 0, 1, 2, 3, 4 },
			                          new[] { (int)(name1 * name2), (int)z, (int)x, (int)c, (int)v });

			var name = context.Registry.GetService<INameService>();
			var marker = context.Registry.GetService<IMarkerService>();
			foreach (IDnlibDef def in members) {
				name.MarkHelper(def, marker, parent);
				if (def is MethodDef)
					parent.ExcludeMethod(context, (MethodDef)def);
			}

			MethodDef cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
			cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, initMethod));

			parent.ExcludeMethod(context, cctor);
		}

		public void HandleMD(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters) {
			methods = parameters.Targets.OfType<MethodDef>().ToList();
			context.CurrentModuleWriterListener.OnWriterEvent += OnWriterEvent;
		}

		void OnWriterEvent(object sender, ModuleWriterListenerEventArgs e) {
			var writer = (ModuleWriterBase)sender;
			if (e.WriterEvent == ModuleWriterEvent.MDEndCreateTables) {
				CreateSections(writer);
			}
			else if (e.WriterEvent == ModuleWriterEvent.BeginStrongNameSign) {
				EncryptSection(writer);
			}
		}

		void CreateSections(ModuleWriterBase writer) {
			var nameBuffer = new byte[8];
			nameBuffer[0] = (byte)(name1 >> 0);
			nameBuffer[1] = (byte)(name1 >> 8);
			nameBuffer[2] = (byte)(name1 >> 16);
			nameBuffer[3] = (byte)(name1 >> 24);
			nameBuffer[4] = (byte)(name2 >> 0);
			nameBuffer[5] = (byte)(name2 >> 8);
			nameBuffer[6] = (byte)(name2 >> 16);
			nameBuffer[7] = (byte)(name2 >> 24);
			var newSection = new PESection(Encoding.ASCII.GetString(nameBuffer), 0xE0000040);
			writer.Sections.Insert(0, newSection); // insert first to ensure proper RVA

			uint alignment;

			alignment = writer.TextSection.Remove(writer.MetaData).Value;
			writer.TextSection.Add(writer.MetaData, alignment);

			alignment = writer.TextSection.Remove(writer.NetResources).Value;
			writer.TextSection.Add(writer.NetResources, alignment);

			alignment = writer.TextSection.Remove(writer.Constants).Value;
			newSection.Add(writer.Constants, alignment);

			// move some PE parts to separate section to prevent it from being hashed
			var peSection = new PESection("", 0x60000020);
			bool moved = false;
			if (writer.StrongNameSignature != null) {
				alignment = writer.TextSection.Remove(writer.StrongNameSignature).Value;
				peSection.Add(writer.StrongNameSignature, alignment);
				moved = true;
			}
			var managedWriter = writer as ModuleWriter;
			if (managedWriter != null) {
				if (managedWriter.ImportAddressTable != null) {
					alignment = writer.TextSection.Remove(managedWriter.ImportAddressTable).Value;
					peSection.Add(managedWriter.ImportAddressTable, alignment);
					moved = true;
				}
				if (managedWriter.StartupStub != null) {
					alignment = writer.TextSection.Remove(managedWriter.StartupStub).Value;
					peSection.Add(managedWriter.StartupStub, alignment);
					moved = true;
				}
			}
			if (moved)
				writer.Sections.Add(peSection);

			// move encrypted methods
			var encryptedChunk = new MethodBodyChunks(writer.TheOptions.ShareMethodBodies);
			newSection.Add(encryptedChunk, 4);
			foreach (MethodDef method in methods) {
				if (!method.HasBody)
					continue;
				MethodBody body = writer.MetaData.GetMethodBody(method);
				bool ok = writer.MethodBodies.Remove(body);
				encryptedChunk.Add(body);
			}

			// padding to prevent bad size due to shift division
			newSection.Add(new ByteArrayChunk(new byte[4]), 4);
		}

		void EncryptSection(ModuleWriterBase writer) {
			Stream stream = writer.DestinationStream;
			var reader = new BinaryReader(writer.DestinationStream);
			stream.Position = 0x3C;
			stream.Position = reader.ReadUInt32();

			stream.Position += 6;
			ushort sections = reader.ReadUInt16();
			stream.Position += 0xc;
			ushort optSize = reader.ReadUInt16();
			stream.Position += 2 + optSize;

			uint encLoc = 0, encSize = 0;
			int origSects = -1;
			if (writer is NativeModuleWriter && writer.Module is ModuleDefMD)
				origSects = ((ModuleDefMD)writer.Module).MetaData.PEImage.ImageSectionHeaders.Count;
			for (int i = 0; i < sections; i++) {
				uint nameHash;
				if (origSects > 0) {
					origSects--;
					stream.Write(new byte[8], 0, 8);
					nameHash = 0;
				}
				else
					nameHash = reader.ReadUInt32() * reader.ReadUInt32();
				stream.Position += 8;
				if (nameHash == name1 * name2) {
					encSize = reader.ReadUInt32();
					encLoc = reader.ReadUInt32();
				}
				else if (nameHash != 0) {
					uint sectSize = reader.ReadUInt32();
					uint sectLoc = reader.ReadUInt32();
					Hash(stream, reader, sectLoc, sectSize);
				}
				else
					stream.Position += 8;
				stream.Position += 16;
			}

			uint[] key = DeriveKey();
			encSize >>= 2;
			stream.Position = encLoc;
			var result = new uint[encSize];
			for (uint i = 0; i < encSize; i++) {
				uint data = reader.ReadUInt32();
				result[i] = data ^ key[i & 0xf];
				key[i & 0xf] = (key[i & 0xf] ^ data) + 0x3dbb2819;
			}
			var byteResult = new byte[encSize << 2];
			Buffer.BlockCopy(result, 0, byteResult, 0, byteResult.Length);
			stream.Position = encLoc;
			stream.Write(byteResult, 0, byteResult.Length);
		}

		void Hash(Stream stream, BinaryReader reader, uint offset, uint size) {
			long original = stream.Position;
			stream.Position = offset;
			size >>= 2;
			for (uint i = 0; i < size; i++) {
				uint data = reader.ReadUInt32();
				uint tmp = (z ^ data) + x + c * v;
				z = x;
				x = c;
				x = v;
				v = tmp;
			}
			stream.Position = original;
		}

		uint[] DeriveKey() {
			uint[] dst = new uint[0x10], src = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				dst[i] = v;
				src[i] = x;
				z = (x >> 5) | (x << 27);
				x = (c >> 3) | (c << 29);
				c = (v >> 7) | (v << 25);
				v = (z >> 11) | (z << 21);
			}
			return deriver.DeriveKey(dst, src);
		}
	}
}