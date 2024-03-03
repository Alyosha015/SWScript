using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWScript.vm.sws_VM;

namespace SWScript.vm {
    internal class sws_Table {
        public Dictionary<object, sws_Variable> Table;
        public Dictionary<object, sws_Variable> Keys;

        public sws_Table() {
            Table = new Dictionary<object, sws_Variable>();
            Keys = new Dictionary<object, sws_Variable>();
        }

        public void Set(sws_Variable i, sws_Variable v) {
            if (Table.ContainsKey(i.Value)) {
                Table[i.Value] = v;
                Keys[i.Value] = i;
            } else {
                Table.Add(i.Value, v);
                Keys.Add(i.Value, i);
            }
        }

        public sws_Variable Get(sws_Variable i) {
            if (i.Type == 0 || i.Value == null) {
                return VAR_NULL;
            }

            if (Table.ContainsKey(i.Value)) {
                return Table[i.Value];
            } else {
                return VAR_NULL;
            }
        }

        public int Size() {
            int count = 1;

            while (true) {
                if(Table.ContainsKey((double)count)) {
                    count++;
                } else {
                    return count - 1;
                }
            }
        }

        public sws_Variable Pairs() {
            sws_Table pairsKeys = new sws_Table();

            sws_Variable[] keys = Keys.Values.ToArray();

            for (int i = 0; i < keys.Length; i++) {
                pairsKeys.Set(new sws_Variable(i + 1), keys[i]);
            }

            return new sws_Variable(pairsKeys, TYPE_TABLE);
        }
    }
}
