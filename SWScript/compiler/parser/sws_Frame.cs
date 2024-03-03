using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScript.compiler {
    internal class sws_Frame {
        public string Name;

        public List<sws_Op> Program;

        public List<sws_Variable> Locals;
        public List<sws_Variable> Upvalues;

        /// <summary>
        /// Stores list of functions in the frame, for which closure generating bytecode is added when the parser finishes parsing the frame.
        /// </summary>
        public List<string> Closures;
    
        public sws_Frame(string name) {
            Name = name;

            Program = new List<sws_Op>();

            Locals = new List<sws_Variable>();
            Upvalues = new List<sws_Variable>();

            Closures = new List<string>();
        }

        public int GetLocal(string name) {
            for (int i = 0; i < Locals.Count; i++) {
                if (Locals[i].Name == name) {
                    return i;
                }
            }

            return -1;
        }
    }
}
