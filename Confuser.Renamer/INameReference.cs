using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;

namespace Confuser.Renamer
{
    public interface INameReference
    {
        bool UpdateNameReference(ConfuserContext context, INameService service);

        bool ShouldCancelRename();
    }

    public interface INameReference<out T> : INameReference
    {
    }
}
