using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer
{
    class PostRenamePhase : ProtectionPhase
    {
        public PostRenamePhase(NameProtection parent)
            : base(parent)
        {
        }

        public override bool ProcessAll { get { return true; } }

        public override ProtectionTargets Targets
        {
            get { return ProtectionTargets.AllDefinitions; }
        }

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            NameService service = (NameService)context.Registry.GetService<INameService>();

            foreach (var renamer in service.Renamers)
            {
                foreach (var def in parameters.Targets)
                    renamer.PostRename(context, service, def);
            }
        }
    }
}
