using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections {
	[BeforeProtection("Ki.ControlFlow")]
	internal class AntiDebugProtection : Protection {
		public const string _Id = "anti debug";
		public const string _FullId = "Ki.AntiDebug";

		public override string Name {
			get { return "Anti Debug Protection"; }
		}

		public override string Description {
			get { return "This protection prevents the assembly from being debugged or profiled."; }
		}

		public override string Id {
			get { return _Id; }
		}

		public override string FullId {
			get { return _FullId; }
		}

		public override ProtectionPreset Preset {
			get { return ProtectionPreset.Minimum; }
		}

		protected override void Initialize(ConfuserContext context) {
			//
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.ProcessModule, new AntiDebugPhase(this));
		}

		class AntiDebugPhase : ProtectionPhase {
			public AntiDebugPhase(AntiDebugProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Modules; }
			}

			public override string Name {
				get { return "Anti-debug injection"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				var rt = context.Registry.GetService<IRuntimeService>();
				var marker = context.Registry.GetService<IMarkerService>();
				var name = context.Registry.GetService<INameService>();

				foreach (ModuleDef module in parameters.Targets.OfType<ModuleDef>()) {
					AntiMode mode = parameters.GetParameter(context, module, "mode", AntiMode.Safe);

					TypeDef rtType;
					TypeDef attr = null;
					const string attrName = "System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute";
					switch (mode) {
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
							foreach (IDnlibDef member in attr.FindDefinitions()) {
								marker.Mark(member, (Protection)Parent);
								name.Analyze(member);
							}
							name.SetCanRename(attr, false);
							break;
						default:
							throw new UnreachableException();
					}

					IEnumerable<IDnlibDef> members = InjectHelper.Inject(rtType, module.GlobalType, module);

					MethodDef cctor = module.GlobalType.FindStaticConstructor();
					var init = (MethodDef)members.Single(method => method.Name == "Initialize");
					cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init));

					foreach (IDnlibDef member in members) {
						marker.Mark(member, (Protection)Parent);
						name.Analyze(member);

						bool ren = true;
						if (member is MethodDef) {
							var method = (MethodDef)member;
							if (method.Access == MethodAttributes.Public)
								method.Access = MethodAttributes.Assembly;
							if (!method.IsConstructor)
								method.IsSpecialName = false;
							else
								ren = false;

							CustomAttribute ca = method.CustomAttributes.Find(attrName);
							if (ca != null)
								ca.Constructor = attr.FindMethod(".ctor");
						}
						else if (member is FieldDef) {
							var field = (FieldDef)member;
							if (field.Access == FieldAttributes.Public)
								field.Access = FieldAttributes.Assembly;
							if (field.IsLiteral) {
								field.DeclaringType.Fields.Remove(field);
								continue;
							}
						}
						if (ren) {
							member.Name = name.ObfuscateName(member.Name, RenameMode.Unicode);
							name.SetCanRename(member, false);
						}
					}
				}
			}

			enum AntiMode {
				Safe,
				Win32,
				Antinet
			}
		}
	}
}