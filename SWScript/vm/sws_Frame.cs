using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScript.vm {
    internal class sws_Frame {
        public string Name;
        public bool LuaCall;

        public int PC;
        public int[] Program;

        public sws_Variable[] Locals;

        public string[] UpvalueFrames;
        public int[] UpvalueIndexes;
        public Dictionary<string, int> UpvalueCSIndexes;

        public bool ReturnValues;

        public sws_Frame() {
            UpvalueCSIndexes = new Dictionary<string, int>();
        }

        public sws_Frame(string name, int[] program, sws_Variable[] locals, string[] upvalueFrames, int[] upvalueIndexes) {
            Name = name;
            Program = program;
            Locals = locals;
            UpvalueFrames = upvalueFrames;
            UpvalueIndexes = upvalueIndexes;

            UpvalueCSIndexes = new Dictionary<string, int>();
        }

        public sws_Frame Copy(sws_Frame frame) {
            Name = frame.Name;
            LuaCall = frame.LuaCall;
            Program = frame.Program;
            PC = frame.PC;

            Locals = new sws_Variable[frame.Locals.Length];
            for (int i = 0; i < frame.Locals.Length; i++) {
                Locals[i] = frame.Locals[i].Clone();
            }

            UpvalueFrames = frame.UpvalueFrames;
            UpvalueIndexes = frame.UpvalueIndexes;
            UpvalueCSIndexes = frame.UpvalueCSIndexes;

            ReturnValues = frame.ReturnValues;

            return this;
        }
    }
}
