using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWS.Compiler {
    internal class sws_TokenizerError : Exception {
        public static bool Suppress;
        public static int Count;

        public static void Reset() {
            Suppress = false;
            Count = 0;
        }

        public string ErrMessage;
        public int Line;
        public int Column;

        public sws_TokenizerError(string message, int line, int column) {
            ErrMessage = message;
            Line = line;
            Column = column;

            if (Suppress) {
                return;
            }

            Count++;
        }
    }
}
