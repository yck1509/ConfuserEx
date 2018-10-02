using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Confuser.Runtime {
	internal static class Compressor {
		static byte[] key;

		static GCHandle Decrypt(uint[] data, uint seed) {
			var w = new uint[0x10];
			var k = new uint[0x10];
			ulong s = seed;
			for (int i = 0; i < 0x10; i++) {
				s = (s * s) % 0x143fc089;
				k[i] = (uint)s;
				w[i] = (uint)((s * s) % 0x444d56fb);
			}
			Mutation.Crypt(w, k);
			Array.Clear(k, 0, 0x10);

			var b = new byte[data.Length << 2];
			uint h = 0;
			for (int i = 0; i < data.Length; i++) {
				uint d = data[i] ^ w[i & 0xf];
				w[i & 0xf] = (w[i & 0xf] ^ d) + 0x3ddb2819;
				b[h + 0] = (byte)(d >> 0);
				b[h + 1] = (byte)(d >> 8);
				b[h + 2] = (byte)(d >> 16);
				b[h + 3] = (byte)(d >> 24);
				h += 4;
			}
			Array.Clear(w, 0, 0x10);
			byte[] j = Lzma.Decompress(b);
			Array.Clear(b, 0, b.Length);

			GCHandle g = GCHandle.Alloc(j, GCHandleType.Pinned);
			var z = (uint)(s % 0x8a5cb7);
			for (int i = 0; i < j.Length; i++) {
				j[i] ^= (byte)s;
				if ((i & 0xff) == 0)
					s = (s * s) % 0x8a5cb7;
			}
			return g;
		}
  [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);
		[STAThread]
		static int Main(string[] args) {
			var l = (uint)Mutation.KeyI0;
			uint[] q = Mutation.Placeholder(new uint[Mutation.KeyI0]);

			Assembly a = Assembly.GetExecutingAssembly();
			Module n = a.ManifestModule;
            GCHandle h = Decrypt(q, (uint)Mutation.KeyI1);
			var b = (byte[])h.Target;
			bool isDebuggerPresent = false;
			      CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isDebuggerPresent);
            if (isDebuggerPresent) Environment.FailFast(null);
            Module m = a.LoadModule("koi", b);
            
            Array.Clear(b, 0, b.Length);
			h.Free();
			Array.Clear(q, 0, q.Length);

			key = n.ResolveSignature(Mutation.KeyI2);
			AppDomain.CurrentDomain.AssemblyResolve += Resolve;

			// For some reasons, reflection on Assembly would not discover the types unless GetTypes is called.
			m.GetTypes();

			MethodBase e = m.ResolveMethod(key[0] | (key[1] << 8) | (key[2] << 16) | (key[3] << 24));
			var g = new object[e.GetParameters().Length];
			if (g.Length != 0)
				g[0] = args;
			object r = e.Invoke(null, g);
			if (r is int)
				return (int)r;
			return 0;
		}

		static Assembly Resolve(object sender, ResolveEventArgs e) {
			byte[] b = Encoding.UTF8.GetBytes(new AssemblyName(e.Name).FullName.ToUpperInvariant());

			Stream m = null;
			if (b.Length + 4 <= key.Length) {
				for (int i = 0; i < b.Length; i++)
					b[i] *= key[i + 4];
				string n = Convert.ToBase64String(b);
				m = Assembly.GetEntryAssembly().GetManifestResourceStream(n);
			}
			if (m != null) {
				var d = new uint[m.Length >> 2];
				var t = new byte[0x100];
				int r;
				int o = 0;
				while ((r = m.Read(t, 0, 0x100)) > 0) {
					Buffer.BlockCopy(t, 0, d, o, r);
					o += r;
				}
				uint s = 0x6fff61;
				foreach (byte c in b)
					s = s * 0x5e3f1f + c;
				GCHandle h = Decrypt(d, s);

				var f = (byte[])h.Target;
				Assembly a = Assembly.Load(f);
				Array.Clear(f, 0, f.Length);
				h.Free();
				Array.Clear(d, 0, d.Length);

				return a;
			}
			return null;
		}
    }
}
