using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScript.vm {
    internal class sws_Variable {
        public object Value;
        public int Type;

        public sws_Variable(object value, int type) {
            Value = value;
            Type = type;
        }

        public sws_Variable(double value) {
            Value = value;
            Type = 2;
        }

        public sws_Variable Clone() {
            return new sws_Variable(Value, Type);
        }

        public override string ToString() {
            object value = Value ?? "null";
            return value.ToString();
        }
    }
}
