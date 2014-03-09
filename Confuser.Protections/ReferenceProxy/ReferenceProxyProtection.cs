using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Protections.ReferenceProxy;

namespace Confuser.Protections
{
    public interface IReferenceProxyService
    {
        void ExcludeMethod(ConfuserContext context, MethodDef method);
    }

    [AfterProtection("Ki.AntiDebug", "Ki.AntiDump")]
    [BeforeProtection("Ki.ControlFlow")]
    class ReferenceProxyProtection : Protection, IReferenceProxyService
    {
        public const string _Id = "ref proxy";
        public const string _FullId = "Ki.RefProxy";
        public const string _ServiceId = "Ki.RefProxy";

        protected override void Initialize(ConfuserContext context)
        {
            context.Registry.RegisterService(_ServiceId, typeof(IReferenceProxyService), this);
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            pipeline.InsertPostStage(PipelineStage.BeginModule, new ReferenceProxyPhase(this));
        }

        public override string Name
        {
            get { return "Reference Proxy Protection"; }
        }

        public override string Description
        {
            get { return "This protection encodes and hides references to type/method/fields."; }
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
            get { return ProtectionPreset.Normal; }
        }

        public void ExcludeMethod(ConfuserContext context, MethodDef method)
        {
            ProtectionParameters.GetParameters(context, method).Remove(this);
        }
    }
}
