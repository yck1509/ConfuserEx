using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;

namespace Confuser.Renamer
{
    public interface IRenamer
    {
        void Analyze(ConfuserContext context, INameService service, IDefinition def);
        void PreRename(ConfuserContext context, INameService service, IDefinition def);
        void PostRename(ConfuserContext context, INameService service, IDefinition def);
    }
}
