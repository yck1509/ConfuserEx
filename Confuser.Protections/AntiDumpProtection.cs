using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using dnlib.DotNet.Emit;
using Confuser.Renamer;

namespace Confuser.Protections
{
    class AntiDumpProtection : Protection
    {
        public const string _Id = "anti dump";
        public const string _FullId = "Ki.AntiDump";

        protected override void Initialize(ConfuserContext context)
        {
            //
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            pipeline.InsertPostStage(PipelineStage.BeginModule, new AntiDumpPhase(this));
        }

        public override string Name
        {
            get { return "Anti Dump Protection"; }
        }

        public override string Description
        {
            get { return "This protection prevents the assembly from being dumped from memory."; }
        }

        public override string Id
        {
            get { return _Id; }
        }

        public override string FullId
        {
            get { return _FullId; }
        }

        public override ProtectionPreset Preset
        {
            get { return ProtectionPreset.Aggressive; }
        }

        class AntiDumpPhase : ProtectionPhase
        {
            public AntiDumpPhase(AntiDumpProtection parent)
                : base(parent)
            {
            }

            public override ProtectionTargets Targets
            {
                get { return ProtectionTargets.Modules; }
            }

            protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
            {
                TypeDef rtType = context.Registry.GetService<IRuntimeService>().GetRuntimeType("Confuser.Runtime.AntiDump");

                IMarkerService marker = context.Registry.GetService<IMarkerService>();
                INameService name = context.Registry.GetService<INameService>();

                foreach (var module in parameters.Targets.OfType<ModuleDef>())
                {
                    var members = InjectHelper.Inject(rtType, module.GlobalType, module);

                    var cctor = module.GlobalType.FindStaticConstructor();
                    var init = (MethodDef)members.Single(method => method.Name == "Initialize");
                    cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init));

                    foreach (var member in members)
                    {
                        ((MethodDef)member).Access = MethodAttributes.PrivateScope;
                        marker.Mark(member);
                        name.Analyze(member);
                    }
                }
            }

        }
    }
}
