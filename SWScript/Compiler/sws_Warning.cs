using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWS.Compiler {
    internal class sws_Warning {
        public static bool Suppress;
        public static int Count;

        public static void Reset() {
            Suppress = false;
            Count = 0;
        }

        public string ErrMessage;
        public sws_Token Token;

        public sws_Warning(sws_Token token, string message) {
            Token = token;
            ErrMessage = message;

            if (Suppress) {
                return;
            }

            Count++;
        }
    }
}
