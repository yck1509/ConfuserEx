using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Project.Patterns
{
    public abstract class PatternOperator : PatternExpression
    {
        public abstract string Name { get; }
        public abstract bool IsUnary { get; }
        public PatternExpression ArgumentA { get; set; }
        public PatternExpression ArgumentB { get; set; }

        public override void Serialize(IList<PatternToken> tokens)
        {
            if (IsUnary)
            {
                tokens.Add(new PatternToken(TokenType.Identifier, Name));
                ArgumentA.Serialize(tokens);
            }
            else
            {
                ArgumentA.Serialize(tokens);
                tokens.Add(new PatternToken(TokenType.Identifier, Name));
                ArgumentB.Serialize(tokens);
            }
        }
    }
}
