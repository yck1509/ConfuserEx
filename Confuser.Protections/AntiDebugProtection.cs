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
    class AntiDebugProtection : Protection
    {
        public const string _Id = "anti debug";
        public const string _FullId = "Ki.AntiDebug";

        protected override void Initialize(ConfuserContext context)
        {
            //
        }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            pipeline.InsertPostStage(PipelineStage.BeginModule, new AntiDebugPhase(this));
        }

        public override string Name
        {
            get { return "Anti Debug Protection"; }
        }

        public override string Description
        {
            get { return "This protection prevents the assembly from being debugged or profiled."; }
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

        class AntiDebugPhase : ProtectionPhase
        {
            public AntiDebugPhase(AntiDebugProtection parent)
                : base(parent)
            {
            }

            public override ProtectionTargets Targets
            {
                get { return ProtectionTargets.Modules; }
            }

            enum AntiMode
            {
                Safe,
                Win32,
                Antinet
            }

            protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
            {
                IRuntimeService rt = context.Registry.GetService<IRuntimeService>();
                IMarkerService marker = context.Registry.GetService<IMarkerService>();
                INameService name = context.Registry.GetService<INameService>();

                foreach (var module in parameters.Targets.OfType<ModuleDef>())
                {
                    AntiMode mode = parameters.GetParameter<AntiMode>(context, module, "mode", AntiMode.Safe);

                    TypeDef rtType;
                    TypeDef attr = null;
                    const string attrName = "System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute";
                    switch (mode)
                    {
                        case AntiMode.Safe:
                            rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugSafe");
                            break;
                        case AntiMode.Win32:
                            rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugWin32");
                            break;
                        case AntiMode.Antinet:
                            rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugAntinet");

                            attr = rt.GetRuntimeType(attrName);
                            module.Types.Add(attr = InjectHelper.Inject(attr, module));
                            foreach (var member in attr.FindDefinitions())
                            {
                                marker.Mark(member);
                                name.Analyze(member);
                            }
                            name.SetCanRename(attr, false);
                            break;
                        default:
                            throw new UnreachableException();
                    }

                    var members = InjectHelper.Inject(rtType, module.GlobalType, module);

                    var cctor = module.GlobalType.FindStaticConstructor();
                    var init = (MethodDef)members.Single(method => method.Name == "Initialize");
                    cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init));

                    foreach (var member in members)
                    {
                        if (member is MethodDef)
                        {
                            name.SetRenameMode(member, RenameMode.Debug);
                            MethodDef method = (MethodDef)member;
                            if (method.Access == MethodAttributes.Public)
                                method.Access = MethodAttributes.Assembly;
                            if (!method.IsConstructor)
                                method.IsSpecialName = false;
                            var ca = method.CustomAttributes.Find(attrName);
                            if (ca != null)
                                ca.Constructor = attr.FindMethod(".ctor");
                        }
                        else if (member is FieldDef)
                        {
                            FieldDef field = (FieldDef)member;
                            if (field.Access == FieldAttributes.Public)
                                field.Access = FieldAttributes.Assembly;
                            if (field.IsLiteral)
                            {
                                field.DeclaringType.Fields.Remove(field);
                                continue;
                            }
                        }
                        marker.Mark(member);
                        name.Analyze(member);
                    }
                }
            }

        }
    }
}
