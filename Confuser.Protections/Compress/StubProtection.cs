using System.Diagnostics;
using System.Security.Cryptography;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.Compress {
	internal class StubProtection : Protection {
		private readonly CompressorContext ctx;

		internal StubProtection(CompressorContext ctx) {
			this.ctx = ctx;
		}

		public override string Name {
			get { return "Compressor Stub Protection"; }
		}

		public override string Description {
			get { return "Do some extra works on the protected stub."; }
		}

		public override string Id {
			get { return "Ki.Compressor.Protection"; }
		}

		public override string FullId {
			get { return "Ki.Compressor.Protection"; }
		}

		public override ProtectionPreset Preset {
			get { return ProtectionPreset.None; }
		}

		protected override void Initialize(ConfuserContext context) {
			//
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.BeginModule, new SigPhase(this));
		}

		private class SigPhase : ProtectionPhase {
			public SigPhase(StubProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Modules; }
			}

			public override string Name {
				get { return "Packer info encoding"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				context.CurrentModuleWriterListener.OnWriterEvent += (sender, e) => {
					                                                     if (e.WriterEvent == ModuleWriterEvent.MDBeginCreateTables) {
						                                                     // Add key signature
						                                                     var writer = (ModuleWriter)sender;
						                                                     var prot = (StubProtection)Parent;
						                                                     uint blob = writer.MetaData.BlobHeap.Add(prot.ctx.KeySig);
						                                                     uint rid = writer.MetaData.TablesHeap.StandAloneSigTable.Add(new RawStandAloneSigRow(blob));
						                                                     Debug.Assert((0x11000000 | rid) == prot.ctx.KeyToken);

						                                                     // Add File reference
						                                                     byte[] hash = SHA1.Create().ComputeHash(prot.ctx.OriginModule);
						                                                     uint hashBlob = writer.MetaData.BlobHeap.Add(hash);

						                                                     MDTable<RawFileRow> fileTbl = writer.MetaData.TablesHeap.FileTable;
						                                                     uint fileRid = fileTbl.Add(new RawFileRow(
							                                                                                (uint)FileAttributes.ContainsMetaData,
							                                                                                writer.MetaData.StringsHeap.Add("koi"),
							                                                                                hashBlob));
					                                                     }
				                                                     };
			}
		}
	}
}