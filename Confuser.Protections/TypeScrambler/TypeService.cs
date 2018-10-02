using Confuser.Core;
using Confuser.Protections.TypeScramble.Scrambler;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Protections.TypeScramble {
    public class TypeService {

        private ConfuserContext content;
        private Dictionary<MDToken, ScannedItem> GenericsMapper = new Dictionary<MDToken, ScannedItem>();
        public static ConfuserContext DebugContext { get; private set; }

        public TypeService(ConfuserContext _context) {
            content = _context;
            DebugContext = content;
        }


        public void AddScannedItem(ScannedMethod m) {

            ScannedItem typescan;
            if(GenericsMapper.TryGetValue(m.TargetMethod.DeclaringType.MDToken, out typescan)) {
                m.GenericCount += typescan.GenericCount;
            }
            AddScannedItemGeneral(m);
        }


        public void AddScannedItem(ScannedType m) {
            //AddScannedItemGeneral(m);
        }

        private void AddScannedItemGeneral(ScannedItem m) {
            m.Scan();
            if (!GenericsMapper.ContainsKey(m.GetToken())) {
                GenericsMapper.Add(m.GetToken(), m);
            }
        }

        public void PrepairItems() {
            foreach(var item in GenericsMapper.Values) {
                item.PrepairGenerics();
            }
        }

        public ScannedItem GetItem(MDToken token) {
            ScannedItem i = null;
            GenericsMapper.TryGetValue(token, out i);
            return i;
        }

    }
}
