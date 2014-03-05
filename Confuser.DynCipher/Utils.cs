using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.DynCipher
{
    static class Utils
    {
        const ulong MODULO32 = 0x100000000;
        static ulong modInv(ulong num, ulong mod)
        {
            ulong a = mod, b = num % mod;
            ulong p0 = 0, p1 = 1;
            while (b != 0)
            {
                if (b == 1) return p1;
                p0 += (a / b) * p1;
                a = a % b;

                if (a == 0) break;
                if (a == 1) return mod - p0;

                p1 += (b / a) * p0;
                b = b % a;
            }
            return 0;
        }

        public static uint modInv(uint num)
        {
            return (uint)modInv(num, MODULO32);
        }
    }
}
