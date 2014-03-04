using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Protections
{
    class AntiILDasmProtection : Protection
    {
        public const string _Id = "anti ildasm";
        public const string _FullId = "Ki.AntiILDasm";

        protected override void Initialize(ConfuserContext context)
        {
            //
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            pipeline.InsertPostStage(PipelineStage.BeginModule, new AntiILDasmPhase(this));
        }

        public override string Name
        {
            get { return "Anti IL Dasm Protection"; }
        }

        public override string Description
        {
            get { return "This protection marks the module with a attribute that discourage ILDasm from disassembling it."; }
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
            get { return ProtectionPreset.Minimum; }
        }

        class AntiILDasmPhase : ProtectionPhase
        {
            public AntiILDasmPhase(AntiILDasmProtection parent)
                : base(parent)
            {
            }

            public override ProtectionTargets Targets
            {
                get { return ProtectionTargets.Modules; }
            }

            protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
            {
                foreach (var module in parameters.Targets.OfType<ModuleDef>())
                {
                    var attrRef = module.CorLibTypes.GetTypeRef("System.Runtime.CompilerServices", "SuppressIldasmAttribute");
                    var ctorRef = new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), attrRef);

                    CustomAttribute attr = new CustomAttribute(ctorRef);
                    module.CustomAttributes.Add(attr);
                }
            }
        }
    }
}
