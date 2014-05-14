using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet.Writer;
using dnlib.DotNet;
using System.IO;
using Confuser.Core.Services;
using System.Diagnostics;
using dnlib.DotNet.MD;
using Confuser.Core.Helpers;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Resources
{
    class MDPhase
    {
        REContext ctx;
        public MDPhase(REContext ctx)
        {
            this.ctx = ctx;
        }

        public void Hook()
        {
            ctx.Context.CurrentModuleWriterListener.OnWriterEvent += OnWriterEvent;
        }

        ByteArrayChunk encryptedResource;

        void OnWriterEvent(object sender, ModuleWriterListenerEventArgs e)
        {
            ModuleWriter writer = (ModuleWriter)sender;
            if (e.WriterEvent == ModuleWriterEvent.MDBeginAddResources)
            {
                var resources = ctx.Module.Resources.OfType<EmbeddedResource>().ToList();
                ctx.Module.Resources.RemoveWhere(res => res is EmbeddedResource);

                // move resources
                var asmName = ctx.Name.RandomName(Confuser.Renamer.RenameMode.Letters);
                var assembly = new AssemblyDefUser(asmName);
                assembly.Modules.Add(new ModuleDefUser(asmName + ".dll"));
                var module = assembly.ManifestModule;
                assembly.ManifestModule.Kind = ModuleKind.Dll;
                var asmRef = new AssemblyRefUser(module.Assembly);
                foreach (var res in resources)
                {
                    res.Attributes = ManifestResourceAttributes.Public;
                    module.Resources.Add(res);
                    ctx.Module.Resources.Add(new AssemblyLinkedResource(res.Name, asmRef, res.Attributes));
                }
                byte[] moduleBuff;
                using (MemoryStream ms = new MemoryStream())
                {
                    module.Write(ms, new ModuleWriterOptions() { StrongNameKey = writer.Options.StrongNameKey });
                    moduleBuff = ms.ToArray();
                }

                // compress
                moduleBuff = ctx.Context.Registry.GetService<ICompressionService>().Compress(moduleBuff);

                uint compressedLen = (uint)(moduleBuff.Length + 3) / 4;
                compressedLen = (compressedLen + 0xfu) & ~0xfu;
                uint[] compressedBuff = new uint[compressedLen];
                Buffer.BlockCopy(moduleBuff, 0, compressedBuff, 0, moduleBuff.Length);
                Debug.Assert(compressedLen % 0x10 == 0);

                // encrypt
                uint keySeed = ctx.Random.NextUInt32() | 0x10;
                uint[] key = new uint[0x10];
                uint state = keySeed;
                for (int i = 0; i < 0x10; i++)
                {
                    state ^= state >> 13;
                    state ^= state << 25;
                    state ^= state >> 27;
                    key[i] = state;
                }

                byte[] encryptedBuffer = new byte[compressedBuff.Length * 4];
                int buffIndex = 0;
                while (buffIndex < compressedBuff.Length)
                {
                    uint[] enc = ctx.ModeHandler.Encrypt(compressedBuff, buffIndex, key);
                    for (int j = 0; j < 0x10; j++)
                        key[j] ^= compressedBuff[buffIndex + j];
                    Buffer.BlockCopy(enc, 0, encryptedBuffer, buffIndex * 4, 0x40);
                    buffIndex += 0x10;
                }
                Debug.Assert(buffIndex == compressedBuff.Length);
                uint size = (uint)encryptedBuffer.Length;

                var tblHeap = writer.MetaData.TablesHeap;
                tblHeap.ClassLayoutTable[writer.MetaData.GetClassLayoutRid(ctx.DataType)].ClassSize = size;
                tblHeap.FieldTable[writer.MetaData.GetRid(ctx.DataField)].Flags |= (ushort)FieldAttributes.HasFieldRVA;
                this.encryptedResource = writer.Constants.Add(new ByteArrayChunk(encryptedBuffer), 8);

                // inject key values
                MutationHelper.InjectKeys(ctx.InitMethod,
                    new int[] { 0, 1 },
                    new int[] { (int)(size / 4), (int)(keySeed) });
            }
            else if (e.WriterEvent == ModuleWriterEvent.EndCalculateRvasAndFileOffsets)
            {
                var tblHeap = writer.MetaData.TablesHeap;
                tblHeap.FieldRVATable[writer.MetaData.GetFieldRVARid(ctx.DataField)].RVA = (uint)encryptedResource.RVA;
            }
        }
    }
}
