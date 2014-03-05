using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.Generation;
using dnlib.DotNet.Emit;

namespace Confuser.DynCipher.AST
{
    public class StatementBlock : Statement
    {
        public StatementBlock()
        {
            this.Statements = new List<Statement>();
        }

        public IList<Statement> Statements { get; private set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            foreach (var i in Statements)
                sb.AppendLine(i.ToString());
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
