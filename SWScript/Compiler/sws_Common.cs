using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWS.Compiler {
    internal class sws_Common {
        public static readonly int JMPFLAG = 262144; //2^18
        //space between these flags reserved for bytecode generator handling bool and/or (which is implemented using conditional jumps to have short-circuiting)
        public static readonly int JMP_CONDITION = 524288; //2^19
        public static readonly int JMP_CONTINUE = 524288 + 1;
        public static readonly int JMP_BREAK = 524288 + 2;
        public static readonly int JMP_IFEND = 524288 + 3;
    }
}
