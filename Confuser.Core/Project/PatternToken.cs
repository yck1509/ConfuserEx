using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Project
{
    public enum TokenType
    {
        // Has Value
        Identifier,
        Literal,

        // No Value
        LParens,
        RParens,
        Comma,
    }
    public struct PatternToken
    {
        public readonly int? Position;
        public readonly TokenType Type;
        public readonly string Value;

        public PatternToken(int pos, TokenType type)
        {
            this.Position = pos;
            this.Type = type;
            this.Value = null;
        }

        public PatternToken(int pos, TokenType type, string value)
        {
            this.Position = pos;
            this.Type = type;
            this.Value = value;
        }

        public PatternToken(TokenType type)
        {
            this.Position = null;
            this.Type = type;
            this.Value = null;
        }

        public PatternToken(TokenType type, string value)
        {
            this.Position = null;
            this.Type = type;
            this.Value = value;
        }

        public override string ToString()
        {
            if (Position != null)
            {
                if (Value != null)
                    return string.Format("[{0}] {1} @ {2}", Type, Value, Position);
                else
                    return string.Format("[{0}] @ {1}", Type, Position);
            }
            else
            {
                if (Value != null)
                    return string.Format("[{0}] {1}", Type, Value);
                else
                    return string.Format("[{0}]", Type);
            }
        }
    }
}
