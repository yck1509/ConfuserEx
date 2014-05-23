using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Confuser.Core.Project
{
    class PatternTokenizer
    {
        string rulePattern;
        int index;

        public void Initialize(string pattern)
        {
            this.rulePattern = pattern;
            this.index = 0;
        }

        void SkipWhitespace()
        {
            while (index < rulePattern.Length && char.IsWhiteSpace(rulePattern[index]))
                index++;
        }

        char? PeekChar()
        {
            if (index >= rulePattern.Length)
                return null;
            return rulePattern[index];
        }

        char NextChar()
        {
            if (index >= rulePattern.Length)
                throw new InvalidPatternException("Unexpected end of pattern.");
            return rulePattern[index++];
        }

        string ReadLiteral()
        {
            StringBuilder ret = new StringBuilder();
            char delim = NextChar();
            Debug.Assert(delim == '"' || delim == '\'');

            char chr = NextChar();
            while (chr != delim)
            {
                // Escape sequence
                if (chr == '\\')
                    ret.Append(NextChar());
                else
                    ret.Append(chr);
                chr = NextChar();
            }
            return ret.ToString();
        }

        string ReadIdentifier()
        {
            StringBuilder ret = new StringBuilder();

            char? chr = PeekChar();
            while (chr != null && (char.IsLetterOrDigit(chr.Value) || chr == '_' || chr == '-'))
            {
                ret.Append(NextChar());
                chr = PeekChar();
            }

            return ret.ToString();
        }

        public PatternToken? NextToken()
        {
            if (rulePattern == null)
                throw new InvalidOperationException("Tokenizer not initialized.");

            SkipWhitespace();
            char? tokenBegin = PeekChar();
            if (tokenBegin == null)
                return null;

            int pos = index;
            switch (tokenBegin.Value)
            {
                case ',':
                    index++;
                    return new PatternToken(pos, TokenType.Comma);
                case '(':
                    index++;
                    return new PatternToken(pos, TokenType.LParens);
                case ')':
                    index++;
                    return new PatternToken(pos, TokenType.RParens);

                case '"':
                case '\'':
                    return new PatternToken(pos, TokenType.Literal, ReadLiteral());

                default:
                    if (!char.IsLetter(tokenBegin.Value))
                        throw new InvalidPatternException(string.Format("Unknown token '{0}' at position {1}.", tokenBegin, pos));

                    return new PatternToken(pos, TokenType.Identifier, ReadIdentifier());
            }
        }
    }
}
