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

namespace Confuser.Protections.AntiTamper {
	internal class JITMode : IModeHandler {
		static readonly CilBody NopBody = new CilBody {
			Instructions = {
				Instruction.Create(OpCodes.Ldnull),
				Instruction.Create(OpCodes.Throw)
			}
		};

		uint c;
		MethodDef cctor;
		MethodDef cctorRepl;
		ConfuserContext context;
		IKeyDeriver deriver;
		byte[] fieldLayout;

		MethodDef initMethod;
		uint key;
		List<MethodDef> methods;
		uint name1, name2;
		RandomGenerator random;
		uint v;
		uint x;
		uint z;

		public void HandleInject(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters) {
			this.context = context;
			random = context.Registry.GetService<IRandomService>().GetRandomGenerator(parent.FullId);
			z = random.NextUInt32();
			x = random.NextUInt32();
			c = random.NextUInt32();
			v = random.NextUInt32();
			name1 = random.NextUInt32() & 0x7f7f7f7f;
			name2 = random.NextUInt32() & 0x7f7f7f7f;
			key = random.NextUInt32();

			fieldLayout = new byte[6];
			for (int i = 0; i < 6; i++) {
				int index = random.NextInt32(0, 6);
				while (fieldLayout[index] != 0)
					index = random.NextInt32(0, 6);
				fieldLayout[index] = (byte)i;
			}

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
			TypeDef initType = rt.GetRuntimeType("Confuser.Runtime.AntiTamperJIT");
			IEnumerable<IDnlibDef> defs = InjectHelper.Inject(initType, context.CurrentModule.GlobalType, context.CurrentModule);
			initMethod = defs.OfType<MethodDef>().Single(method => method.Name == "Initialize");

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

			cctor = context.CurrentModule.GlobalType.FindStaticConstructor();

			cctorRepl = new MethodDefUser(name.RandomName(), MethodSig.CreateStatic(context.CurrentModule.CorLibTypes.Void));
			cctorRepl.IsStatic = true;
			cctorRepl.Access = MethodAttributes.CompilerControlled;
			cctorRepl.Body = new CilBody();
			cctorRepl.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
			context.CurrentModule.GlobalType.Methods.Add(cctorRepl);
			name.MarkHelper(cctorRepl, marker, parent);

			MutationHelper.InjectKeys(defs.OfType<MethodDef>().Single(method => method.Name == "HookHandler"),
			                          new[] { 0 }, new[] { (int)key });
			foreach (IDnlibDef def in defs) {
				if (def.Name == "MethodData") {
					var dataType = (TypeDef)def;
					FieldDef[] fields = dataType.Fields.ToArray();
					var layout = fieldLayout.Clone() as byte[];
					Array.Sort(layout, fields);
					for (byte j = 0; j < 6; j++)
						layout[j] = j;
					Array.Sort(fieldLayout, layout);
					fieldLayout = layout;
					dataType.Fields.Clear();
					foreach (FieldDef f in fields)
						dataType.Fields.Add(f);
				}
				name.MarkHelper(def, marker, parent);
				if (def is MethodDef)
					parent.ExcludeMethod(context, (MethodDef)def);
			}
			parent.ExcludeMethod(context, cctor);
		}

		public void HandleMD(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters) {
			// move initialization away from module initializer
			cctorRepl.Body = cctor.Body;
			cctor.Body = new CilBody();
			cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, initMethod));
			cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, cctorRepl));
			cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

			methods = parameters.Targets.OfType<MethodDef>().Where(method => method.HasBody).ToList();
			context.CurrentModuleWriterListener.OnWriterEvent += OnWriterEvent;
		}

		void OnWriterEvent(object sender, ModuleWriterListenerEventArgs e) {
			var writer = (ModuleWriterBase)sender;
			if (e.WriterEvent == ModuleWriterEvent.MDBeginWriteMethodBodies) {
				context.Logger.Debug("Extracting method bodies...");
				CreateSection(writer);
			}
			else if (e.WriterEvent == ModuleWriterEvent.BeginStrongNameSign) {
				context.Logger.Debug("Encrypting method section...");
				EncryptSection(writer);
			}
		}

		void CreateSection(ModuleWriterBase writer) {
			// move some PE parts to separate section to prevent it from being hashed
			var peSection = new PESection("", 0x60000020);
			bool moved = false;
			uint alignment;
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

			// create section
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
			writer.Sections.Insert(random.NextInt32(writer.Sections.Count), newSection);

			// random padding at beginning to prevent revealing hash key
			newSection.Add(new ByteArrayChunk(random.NextBytes(0x10)), 0x10);

			// create index
			var bodyIndex = new JITBodyIndex(methods.Select(method => writer.MetaData.GetToken(method).Raw));
			newSection.Add(bodyIndex, 0x10);

			// save methods
			foreach (MethodDef method in methods.WithProgress(context.Logger)) {
				if (!method.HasBody)
					continue;

				MDToken token = writer.MetaData.GetToken(method);

				var jitBody = new JITMethodBody();
				var bodyWriter = new JITMethodBodyWriter(writer.MetaData, method.Body, jitBody, random.NextUInt32(), writer.MetaData.KeepOldMaxStack || method.Body.KeepOldMaxStack);
				bodyWriter.Write();
				jitBody.Serialize(token.Raw, key, fieldLayout);
				bodyIndex.Add(token.Raw, jitBody);

				method.Body = NopBody;
				writer.MetaData.TablesHeap.MethodTable[token.Rid].ImplFlags |= (ushort)MethodImplAttributes.NoInlining;
				context.CheckCancellation();
			}
			bodyIndex.PopulateSection(newSection);

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