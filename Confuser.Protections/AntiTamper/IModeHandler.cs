using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;

namespace Confuser.Protections.AntiTamper
{
    interface IModeHandler
    {
        void HandleInject(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters);
        void HandleMD(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters);
    }
}
