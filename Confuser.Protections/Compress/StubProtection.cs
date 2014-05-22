using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet.Writer;
using dnlib.DotNet.MD;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Confuser.Protections.Compress
{
    class StubProtection : Protection
    {
        CompressorContext ctx;
        internal StubProtection(CompressorContext ctx)
        {
            this.ctx = ctx;
        }

        protected override void Initialize(ConfuserContext context)
        {
            //
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            pipeline.InsertPostStage(PipelineStage.BeginModule, new SigPhase(this));
        }

        public override string Name
        {
            get { return "Compressor Stub Protection"; }
        }

        public override string Description
        {
            get { return "Do some extra works on the protected stub."; }
        }

        public override string Id
        {
            get { return "Ki.Compressor.Protection"; }
        }

        public override string FullId
        {
            get { return "Ki.Compressor.Protection"; }
        }

        public override ProtectionPreset Preset
        {
            get { return ProtectionPreset.None; }
        }

        class SigPhase : ProtectionPhase
        {
            public SigPhase(StubProtection parent)
                : base(parent)
            {
            }

            public override ProtectionTargets Targets
            {
                get { return ProtectionTargets.Modules; }
            }

            protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
            {
                context.CurrentModuleWriterListener.OnWriterEvent += (sender, e) =>
                {
                    if (e.WriterEvent == ModuleWriterEvent.MDBeginCreateTables)
                    {
                        // Add key signature
                        var writer = (ModuleWriter)sender;
                        var prot = (StubProtection)Parent;
                        uint blob = writer.MetaData.BlobHeap.Add(prot.ctx.KeySig);
                        uint rid = writer.MetaData.TablesHeap.StandAloneSigTable.Add(new RawStandAloneSigRow(blob));
                        Debug.Assert((0x11000000 | rid) == prot.ctx.KeyToken);

                        // Add File reference
                        var hash = SHA1Managed.Create().ComputeHash(prot.ctx.OriginModule);
                        var hashBlob = writer.MetaData.BlobHeap.Add(hash);

                        var fileTbl = writer.MetaData.TablesHeap.FileTable;
                        var fileRid = fileTbl.Add(new RawFileRow(
                            (uint)dnlib.DotNet.FileAttributes.ContainsMetaData,
                            writer.MetaData.StringsHeap.Add("koi"),
                            hashBlob));
                    }
                };
            }
        }
    }
}
