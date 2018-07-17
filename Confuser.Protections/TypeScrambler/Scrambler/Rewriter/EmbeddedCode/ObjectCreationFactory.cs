using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.EmbeddedCode {
    public class ObjectCreationFactory {

        public static void Import(ModuleDef mod) {

            var curMod = ModuleDefMD.Load(typeof(ObjectCreationFactory).Module);
            
            var t = curMod.Find(typeof(ObjectCreationFactory).FullName, true);
            curMod.Types.Remove(t);

            var newT = new TypeDefUser("ObjectCreationFactory");
            var methods = t.Methods.ToArray();
            foreach (var m in methods) {
                m.DeclaringType = null;
                newT.Methods.Add(m);
            }

            mod.Types.Add(t);
          //  return newT;
        }

        public static T Create<T>() {
            return Activator.CreateInstance<T>();
        }
        public static TR Create<TR, T0>(T0 p0) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0 });
        }

        public static TR Create<TR, T0, T1>(T0 p0, T1 p1) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1 });
        }

        public static TR Create<TR, T0, T1, T2>(T0 p0, T1 p1, T2 p2) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2 });
        }

        public static TR Create<TR, T0, T1, T2, T3>(T0 p0, T1 p1, T2 p2, T3 p3) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14, T15 p15) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14, T15 p15, T16 p16) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14, T15 p15, T16 p16, T17 p17) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17 });
        }

        public static TR Create<TR, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18>(T0 p0, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8, T9 p9, T10 p10, T11 p11, T12 p12, T13 p13, T14 p14, T15 p15, T16 p16, T17 p17, T18 p18) {
            return (TR)Activator.CreateInstance(typeof(TR), new object[] { p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18 });
        }
    }
}
