using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core.Project
{
    public class InvalidPatternException : Exception
    {
        public InvalidPatternException(string message)
            : base(message)
        {
        }
        public InvalidPatternException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
