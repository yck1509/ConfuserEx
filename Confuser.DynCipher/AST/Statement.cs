using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.AST
{
    public abstract class Statement
    {
        public abstract override string ToString();
        public object Tag { get; set; }
    }
}
