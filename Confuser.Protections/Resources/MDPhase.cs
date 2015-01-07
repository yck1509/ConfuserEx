using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.Resources {
	internal class MDPhase {
		readonly REContext ctx;
		ByteArrayChunk encryptedResource;

		public MDPhase(REContext ctx) {
			this.ctx = ctx;
		}

		public void Hook() {
			ctx.Context.CurrentModuleWriterListener.OnWriterEvent += OnWriterEvent;
		}

		void OnWriterEvent(object sender, ModuleWriterListenerEventArgs e) {
			var writer = (ModuleWriterBase)sender;
			if (e.WriterEvent == ModuleWriterEvent.MDBeginAddResources) {
				ctx.Context.CheckCancellation();
				ctx.Context.Logger.Debug("Encrypting resources...");
				bool hasPacker = ctx.Context.Packer != null;

				List<EmbeddedResource> resources = ctx.Module.Resources.OfType<EmbeddedResource>().ToList();
				if (!hasPacker)
					ctx.Module.Resources.RemoveWhere(res => res is EmbeddedResource);

				// move resources
				string asmName = ctx.Name.RandomName(RenameMode.Letters);
				PublicKey pubKey = null;
				if (writer.TheOptions.StrongNameKey != null)
					pubKey = PublicKeyBase.CreatePublicKey(writer.TheOptions.StrongNameKey.PublicKey);
				var assembly = new AssemblyDefUser(asmName, new Version(0, 0), pubKey);
				assembly.Modules.Add(new ModuleDefUser(asmName + ".dll"));
				ModuleDef module = assembly.ManifestModule;
				assembly.ManifestModule.Kind = ModuleKind.Dll;
				var asmRef = new AssemblyRefUser(module.Assembly);
				if (!hasPacker) {
					foreach (EmbeddedResource res in resources) {
						res.Attributes = ManifestResourceAttributes.Public;
						module.Resources.Add(res);
						ctx.Module.Resources.Add(new AssemblyLinkedResource(res.Name, asmRef, res.Attributes));
					}
				}
				byte[] moduleBuff;
				using (var ms = new MemoryStream()) {
					module.Write(ms, new ModuleWriterOptions { StrongNameKey = writer.TheOptions.StrongNameKey });
					moduleBuff = ms.ToArray();
				}

				// compress
				moduleBuff = ctx.Context.Registry.GetService<ICompressionService>().Compress(
					moduleBuff,
					progress => ctx.Context.Logger.Progress((int)(progress * 10000), 10000));
				ctx.Context.Logger.EndProgress();
				ctx.Context.CheckCancellation();

				uint compressedLen = (uint)(moduleBuff.Length + 3) / 4;
				compressedLen = (compressedLen + 0xfu) & ~0xfu;
				var compressedBuff = new uint[compressedLen];
				Buffer.BlockCopy(moduleBuff, 0, compressedBuff, 0, moduleBuff.Length);
				Debug.Assert(compressedLen % 0x10 == 0);

				// encrypt
				uint keySeed = ctx.Random.NextUInt32() | 0x10;
				var key = new uint[0x10];
				uint state = keySeed;
				for (int i = 0; i < 0x10; i++) {
					state ^= state >> 13;
					state ^= state << 25;
					state ^= state >> 27;
					key[i] = state;
				}

				var encryptedBuffer = new byte[compressedBuff.Length * 4];
				int buffIndex = 0;
				while (buffIndex < compressedBuff.Length) {
					uint[] enc = ctx.ModeHandler.Encrypt(compressedBuff, buffIndex, key);
					for (int j = 0; j < 0x10; j++)
						key[j] ^= compressedBuff[buffIndex + j];
					Buffer.BlockCopy(enc, 0, encryptedBuffer, buffIndex * 4, 0x40);
					buffIndex += 0x10;
				}
				Debug.Assert(buffIndex == compressedBuff.Length);
				var size = (uint)encryptedBuffer.Length;

				TablesHeap tblHeap = writer.MetaData.TablesHeap;
				tblHeap.ClassLayoutTable[writer.MetaData.GetClassLayoutRid(ctx.DataType)].ClassSize = size;
				tblHeap.FieldTable[writer.MetaData.GetRid(ctx.DataField)].Flags |= (ushort)FieldAttributes.HasFieldRVA;
				encryptedResource = writer.Constants.Add(new ByteArrayChunk(encryptedBuffer), 8);

				// inject key values
				MutationHelper.InjectKeys(ctx.InitMethod,
				                          new[] { 0, 1 },
				                          new[] { (int)(size / 4), (int)(keySeed) });
			}
			else if (e.WriterEvent == ModuleWriterEvent.EndCalculateRvasAndFileOffsets) {
				TablesHeap tblHeap = writer.MetaData.TablesHeap;
				tblHeap.FieldRVATable[writer.MetaData.GetFieldRVARid(ctx.DataField)].RVA = (uint)encryptedResource.RVA;
			}
		}
	}
}