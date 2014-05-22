using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace Confuser.Runtime
{
    static class Compressor
    {
        static byte[] key;

        static GCHandle Decrypt(uint[] data, uint seed)
        {
            uint[] w = new uint[0x10];
            uint[] k = new uint[0x10];
            ulong s = seed;
            for (int i = 0; i < 0x10; i++)
            {
                s = (s * s) % 0x143fc089;
                k[i] = (uint)s;
                w[i] = (uint)((s * s) % 0x444d56fb);
            }
            Mutation.Crypt(w, k);
            Array.Clear(k, 0, 0x10);

            byte[] b = new byte[data.Length << 2];
            uint h = 0;
            for (int i = 0; i < data.Length; i++)
            {
                uint d = data[i] ^ w[i & 0xf];
                w[i & 0xf] = (w[i & 0xf] ^ d) + 0x3ddb2819;
                b[h + 0] = (byte)(d >> 0);
                b[h + 1] = (byte)(d >> 8);
                b[h + 2] = (byte)(d >> 16);
                b[h + 3] = (byte)(d >> 24);
                h += 4;
            }
            Array.Clear(w, 0, 0x10);
            var j = Lzma.Decompress(b);
            Array.Clear(b, 0, b.Length);

            GCHandle g = GCHandle.Alloc(j, GCHandleType.Pinned);
            uint z = (uint)(s % 0x8a5cb7);
            for (int i = 0; i < j.Length; i++)
            {
                j[i] ^= (byte)s;
                if ((i & 0xff) == 0)
                    s = (s * s) % 0x8a5cb7;
            }
            return g;
        }

        static int Main(string[] args)
        {
            uint l = (uint)Mutation.KeyI0;
            uint[] q = Mutation.Placeholder(new uint[Mutation.KeyI0]);

            var h = Decrypt(q, (uint)Mutation.KeyI1);
            var b = (byte[])h.Target;
            var a = Assembly.GetExecutingAssembly();
            var n = a.ManifestModule;
            var m = a.LoadModule("koi", b);
            Array.Clear(b, 0, b.Length);
            h.Free();
            Array.Clear(q, 0, q.Length);

            key = n.ResolveSignature(Mutation.KeyI2);
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

            var e = m.ResolveMethod(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
            object[] g = new object[e.GetParameters().Length];
            if (g.Length != 0)
                g[0] = args;
            var r = e.Invoke(null, g);
            if (r is int)
                return (int)r;
            else
                return 0;
        }

        static Assembly Resolve(object sender, ResolveEventArgs e)
        {
            byte[] b = Encoding.UTF8.GetBytes(e.Name);
            for (int i = 0; i < b.Length; i++)
                b[i] *= key[i + 4];
            var n = Convert.ToBase64String(b);
            var m = Assembly.GetEntryAssembly().GetManifestResourceStream(n);
            if (m != null)
            {
                uint[] d = new uint[m.Length >> 2];
                byte[] t = new byte[0x100];
                int r;
                int o = 0;
                while ((r = m.Read(t, 0, 0x100)) > 0)
                {
                    Buffer.BlockCopy(t, 0, d, o, r);
                    o += r;
                }
                uint s = 0x6fff61;
                foreach (var c in b)
                    s = s * 0x5e3f1f + c;
                var h = Decrypt(d, s);

                var f = (byte[])h.Target;
                var a = Assembly.Load(f);
                Array.Clear(f, 0, f.Length);
                h.Free();
                Array.Clear(d, 0, d.Length);

                return a;
            }
            return null;
        }
    }
}
