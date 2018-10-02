using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest
{
    internal class GenericTest<T> where T : IEnumerable<Char>
    {
        public IEnumerable<Char> GetReverse(T input) => input.Reverse();
    }
}
