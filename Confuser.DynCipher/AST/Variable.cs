using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.DynCipher.AST
{
    public class Variable
    {
        public Variable(string name)
        {
            this.Name = name;
        }
        public string Name { get; set; }
        public object Tag { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
