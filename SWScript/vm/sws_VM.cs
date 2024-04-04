using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SWScript.compiler;

using static SWScript.compiler.sws_Opcode;

namespace SWScript.vm {
    internal class sws_VM {
        internal sws_Prg Prg;

        public string ProgramHex;
        public string Program;

        //old programming trick, use a power of 2 so that it doesn't seem like you're making up random numbers.
        public const int STACK_SIZE = 65536;
        public const int CALLSTACK_SIZE = 65536;

        public const int TYPE_NULL = 0;
        public const int TYPE_BOOL = 1;
        public const int TYPE_DOUBLE = 2;
        public const int TYPE_STRING = 3;
        public const int TYPE_TABLE = 4;
        public const int TYPE_CLOSURE = 5;

        public static readonly sws_Variable VAR_NULL = new sws_Variable(null, TYPE_NULL);
        public static readonly sws_Variable VAR_FALSE = new sws_Variable(false, TYPE_BOOL);
        public static readonly sws_Variable VAR_TRUE = new sws_Variable(true, TYPE_BOOL);

        //
        internal Dictionary<string, sws_Frame> Frames;
        internal sws_Frame Frame;

        internal sws_Variable[] Stack;
        internal int SP;
        internal sws_Frame[] CallStack;
        internal int CSP;

        internal sws_Variable[] Globals;
        internal sws_Variable[] Constants;

        internal bool hasOnTickFunc;
        internal bool hasOnDrawFunc;

        public sws_VM(sws_Prg prg) {
            Prg = prg;

            ProgramHex = Prg.Program;

            Frames = new Dictionary<string, sws_Frame>();

            Stack = new sws_Variable[STACK_SIZE];

            CallStack = new sws_Frame[CALLSTACK_SIZE];
            for (int i = 0; i < CALLSTACK_SIZE; i++) {
                CallStack[i] = new sws_Frame();
            }

            Load();

            hasOnTickFunc = Frames.ContainsKey("onTick");
            hasOnDrawFunc = Frames.ContainsKey("onDraw");
        }

        public void Load() {
            //convert hex string back to bytes
            for (int i = 0; i < ProgramHex.Length / 2; i++) {
                string hexByte = ProgramHex.Substring(i * 2, 2);
                Program += (char)int.Parse(hexByte, System.Globalization.NumberStyles.HexNumber);
            }

            int current = 0;

            //read frame data
            int frameCount = NextInt();
            for (int i = 0; i < frameCount; i++) {
                string name = Next();

                int programCount = NextInt();
                int[] program = new int[programCount];
                for (int j = 0; j < programCount; j++) {
                    program[j] = NextInt();
                }

                int localCount = NextInt();
                sws_Variable[] locals = new sws_Variable[localCount];
                for (int j = 0; j < locals.Length; j++) {
                    locals[j] = VAR_NULL;
                }

                int upvalueCount = NextInt();
                string[] frameReferences = new string[upvalueCount];
                int[] indexReferences = new int[upvalueCount];
                for (int j = 0; j < upvalueCount; j++) {
                    frameReferences[j] = Next();
                    indexReferences[j] = NextInt();
                }
                
                sws_Variable[] upvalues = new sws_Variable[upvalueCount];
                for (int j = 0; j < upvalues.Length; j++) {
                    upvalues[j] = VAR_NULL;
                }

                sws_Frame frame = new sws_Frame(name, program, locals, upvalues, frameReferences, indexReferences);
                Frames.Add(name, frame);
            }

            //globals
            int globalsCount = NextInt();
            Globals = new sws_Variable[globalsCount];
            for (int i = 0; i < globalsCount; i++) {
                Globals[i] = VAR_NULL;
            }

            //constants
            int constCount = NextInt();
            Constants = new sws_Variable[constCount];
            for (int i = 0; i < constCount; i++) {
                string data = Next();
                int type = NextInt();

                //(only doubles and strings are stored in constants)
                object value = data;

                if (type == TYPE_DOUBLE) {
                    value = double.Parse(data);
                }

                Constants[i] = new sws_Variable(value, type);
            }

            string Next() {
                int index = Program.Substring(current).IndexOf((char)1);
                string s = Program.Substring(current, index);
                current += index + 1;
                return s;
            }

            int NextInt() {
                return int.Parse(Next());
            }
        }

        public void Run() {
            try {
                bool start = false;

                string name = string.Empty;
                string last = string.Empty;

                Frame = new sws_Frame();

                while (true) {
                    //set where to begin execution
                    if (!start) { //runs main
                        CSP = 1;
                        start = true;
                    } else if (hasOnTickFunc && (last != "onTick" || !hasOnDrawFunc)) {
                        name = "onTick";
                    } else if (hasOnDrawFunc && (last != "onDraw" || !hasOnTickFunc)) {
                        name = "onDraw";
                    }

                    //load stack frame
                    Frame.Copy(Frames[name]);

                    //load upvalues for onTick and onDraw frames, this is done for other function calls in op_call.
                    if (name == "onTick" || name == "onDraw") {
                        CallStack[1].Copy(Frame);
                        if (Array.IndexOf(Frame.UpvalueFrames, CallStack[0].Name) != -1) {
                            if (!Frame.UpvalueCSIndexes.ContainsKey(CallStack[0].Name)) {
                                Frame.UpvalueCSIndexes.Add(CallStack[0].Name, 0);
                            }
                        }
                    } else {
                        //runs once, when n=""
                        CallStack[0].Copy(Frame);
                    }

                    bool halt = false;
                    //execute bytecode until halt or return from onTick/onDraw
                    while (true) {
                        int instruction = Frame.Program[Frame.PC++];

                        int op = instruction >> 18;

                        int data = instruction & 0b01_1111_1111_1111_1111;
                        bool negative = false;

                        if ((instruction & 0b10_0000_0000_0000_0000) > 0) {
                            data = -data;
                            negative = true;
                        }

                        switch (op) {
                            case (int)op_getconst: {
                                    Push(Constants[data]);
                                    break;
                                }
                            case (int)op_getlocal: {
                                    Push(Frame.Locals[data]);
                                    break;
                                }
                            case (int)op_getupval: {
                                    if (Frame.UpvalueCSIndexes.ContainsKey(Frame.UpvalueFrames[data])) {
                                        Push(CallStack[Frame.UpvalueCSIndexes[Frame.UpvalueFrames[data]]].Locals[Frame.UpvalueIndexes[data]]);
                                    } else {
                                        Push(Frame.Upvalues[data]);
                                    }
                                    break;
                                }
                            case (int)op_getglobal: {
                                    Push(Globals[data]);
                                    break;
                                }
                            case (int)op_setlocal: {
                                    Frame.Locals[data] = Pop();
                                    break;
                                }
                            case (int)op_setupval: {
                                    if (Frame.UpvalueCSIndexes.ContainsKey(Frame.UpvalueFrames[data])) {
                                        CallStack[Frame.UpvalueCSIndexes[Frame.UpvalueFrames[data]]].Locals[Frame.UpvalueIndexes[data]] = Pop();
                                    } else {
                                        Frame.Upvalues[data] = Pop();
                                    }
                                    break;
                                }
                            case (int)op_setglobal: {
                                    Globals[data] = Pop();
                                    break;
                                }
                            case (int)op_dup: {
                                    sws_Variable variable = Pop();
                                    Push(variable);
                                    Push(variable);
                                    break;
                                }
                            case (int)op_loadimm: {
                                    Push(new sws_Variable(data));
                                    break;
                                }
                            case (int)op_loadbool: {
                                    if (data == 1) {
                                        Push(VAR_TRUE);
                                    } else {
                                        Push(VAR_FALSE);
                                    }
                                    break;
                                }
                            case (int)op_loadnull: {
                                    Push(VAR_NULL);
                                    break;
                                }
                            case (int)op_add: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    Push(new sws_Variable(a + b));
                                    break;
                                }
                            case (int)op_sub: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    Push(new sws_Variable(a - b));
                                    break;
                                }
                            case (int)op_mul: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    Push(new sws_Variable(a * b));
                                    break;
                                }
                            case (int)op_div: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    Push(new sws_Variable(a / b));
                                    break;
                                }
                            case (int)op_floordiv: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    Push(new sws_Variable((double)((long)(a / b) - Convert.ToInt32(((a < 0) ^ (b < 0)) && (a % b != 0)))));
                                    break;
                                }
                            case (int)op_pow: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    Push(new sws_Variable(Math.Pow(a, b)));
                                    break;
                                }
                            case (int)op_mod: {
                                    double dividend = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double divisor = (double)PopTypeCheck(TYPE_DOUBLE);

                                    double c = dividend % divisor;
                                    if (c < 0 && divisor > 0 || c > 0 && divisor < 0) {
                                        c += divisor;
                                    }

                                    Push(new sws_Variable(c));
                                    break;
                                }
                            case (int)op_bitand: {
                                    long a = PopInt64();
                                    long b = PopInt64();

                                    Push(new sws_Variable(a & b));
                                    break;
                                }
                            case (int)op_bitor: {
                                    long a = PopInt64();
                                    long b = PopInt64();

                                    Push(new sws_Variable(a | b));
                                    break;
                                }
                            case (int)op_bitxor: {
                                    long a = PopInt64();
                                    long b = PopInt64();

                                    Push(new sws_Variable(a ^ b));
                                    break;
                                }
                            case (int)op_bitshiftleft: {
                                    long a = PopInt64();
                                    long b = PopInt64();

                                    Push(new sws_Variable(a << (int)b));
                                    break;
                                }
                            case (int)op_bitshiftright: {
                                    long a = PopInt64();
                                    long b = PopInt64();

                                    Push(new sws_Variable(a >> (int)b));
                                    break;
                                }
                            case (int)op_addimm: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    Push(new sws_Variable(a + data));
                                    break;
                                }
                            case (int)op_minus: {
                                    Push(new sws_Variable(-(double)PopTypeCheck(TYPE_DOUBLE)));
                                    break;
                                }
                            case (int)op_boolnot: {
                                    Push(new sws_Variable(!(bool)PopTypeCheck(TYPE_BOOL), TYPE_BOOL));
                                    break;
                                }
                            case (int)op_bitnot: {
                                    Push(new sws_Variable(~PopInt64()));
                                    break;
                                }
                            case (int)op_len: {
                                    sws_Variable a = Pop();
                                    if (a.Type == TYPE_STRING) {
                                        Push(new sws_Variable(a.Value.ToString().Length));
                                    } else if (a.Type == TYPE_TABLE) {
                                        sws_Table t = (sws_Table)a.Value;
                                        Push(new sws_Variable(t.Size()));
                                    } else {
                                        throw new sws_VMError($"Unexpected type {TypeToString(a.Type)} for length operation. Expected string or table.", Prg, Frame.Name, Frame.PC - 1);
                                    }
                                    break;
                                }
                            case (int)op_concat: {
                                    object a = Pop().Value ?? "null";
                                    object b = Pop().Value ?? "null";

                                    string str = a.ToString() + b.ToString(); //concat two values by default

                                    for (int i = 0; i < data; i++) {
                                        object c = Pop().Value ?? "null";
                                        str += c.ToString();
                                    }

                                    Push(new sws_Variable(str, TYPE_STRING));
                                    break;
                                }
                            case (int)op_eq: {
                                    sws_Variable a = Pop();
                                    sws_Variable b = Pop();

                                    switch (a.Type) {
                                        case TYPE_NULL: {
                                                if (b.Value == null) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_BOOL: {
                                                if (TypeCheck(b, TYPE_BOOL, true)) {
                                                    Push(a.Value == null ? VAR_TRUE : VAR_FALSE);
                                                    break;
                                                }
                                                if ((bool)a.Value == (bool)b.Value) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_DOUBLE: {
                                                if (TypeCheck(b, TYPE_DOUBLE, true)) {
                                                    Push(a.Value == null ? VAR_TRUE : VAR_FALSE);
                                                    break;
                                                }
                                                if ((double)a.Value == (double)b.Value) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_STRING: {
                                                if (TypeCheck(b, TYPE_STRING, true)) {
                                                    Push(a.Value == null ? VAR_TRUE : VAR_FALSE);
                                                    break;
                                                }
                                                if (a.Value.ToString() == b.Value.ToString()) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_TABLE: {
                                                if (TypeCheck(b, TYPE_TABLE, true)) {
                                                    Push(a.Value == null ? VAR_TRUE : VAR_FALSE);
                                                    break;
                                                }
                                                if (a.Value == b.Value) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_CLOSURE: {
                                                if (TypeCheck(b, TYPE_CLOSURE, true)) {
                                                    Push(a.Value == null ? VAR_TRUE : VAR_FALSE);
                                                    break;
                                                }
                                                if (a.Value == b.Value) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case (int)op_neq: {
                                    sws_Variable a = Pop();
                                    sws_Variable b = Pop();

                                    switch (a.Type) {
                                        case TYPE_NULL: {
                                                if (b.Value != null) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_BOOL: {
                                                if (TypeCheck(b, TYPE_BOOL, true)) {
                                                    Push(a.Value == null ? VAR_FALSE : VAR_TRUE);
                                                    break;
                                                }
                                                if ((bool)a.Value != (bool)b.Value) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_DOUBLE: {
                                                if (TypeCheck(b, TYPE_DOUBLE, true)) {
                                                    Push(a.Value == null ? VAR_FALSE : VAR_TRUE);
                                                    break;
                                                }
                                                if ((double)a.Value != (double)b.Value) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_STRING: {
                                                if (TypeCheck(b, TYPE_STRING, true)) {
                                                    Push(a.Value == null ? VAR_FALSE : VAR_TRUE);
                                                    break;
                                                }
                                                if (a.Value.ToString() != b.Value.ToString()) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_TABLE: {
                                                if (TypeCheck(b, TYPE_TABLE, true)) {
                                                    Push(a.Value == null ? VAR_FALSE : VAR_TRUE);
                                                    break;
                                                }
                                                if (a.Value != b.Value) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                        case TYPE_CLOSURE: {
                                                if (TypeCheck(b, TYPE_CLOSURE, true)) {
                                                    Push(a.Value == null ? VAR_FALSE : VAR_TRUE);
                                                    break;
                                                }
                                                if (a.Value != b.Value) {
                                                    Push(VAR_TRUE);
                                                } else {
                                                    Push(VAR_FALSE);
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case (int)op_lt: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    Push(a < b ? VAR_TRUE : VAR_FALSE);
                                    break;
                                }
                            case (int)op_lte: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    Push(a <= b ? VAR_TRUE : VAR_FALSE);
                                    break;
                                }
                            case (int)op_jmp: {
                                    Frame.PC += data;
                                    break;
                                }
                            case (int)op_jeq: {
                                    sws_Variable a = Pop();
                                    sws_Variable b = Pop();

                                    switch (a.Type) {
                                        case TYPE_NULL: {
                                                if (b.Value == null) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_BOOL: {
                                                if (TypeCheck(b, TYPE_BOOL, true)) {
                                                    if (a.Value == null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if ((bool)a.Value == (bool)b.Value) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_DOUBLE: {
                                                if (TypeCheck(b, TYPE_DOUBLE, true)) {
                                                    if (a.Value == null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if ((double)a.Value == (double)b.Value) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_STRING: {
                                                if (TypeCheck(b, TYPE_STRING, true)) {
                                                    if (a.Value == null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if (a.Value.ToString() == b.Value.ToString()) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_TABLE: {
                                                if (TypeCheck(b, TYPE_TABLE, true)) {
                                                    if (a.Value == null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if (a.Value == b.Value) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_CLOSURE: {
                                                if (TypeCheck(b, TYPE_CLOSURE, true)) {
                                                    if (a.Value == null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if (a.Value == b.Value) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case (int)op_jneq: {
                                    sws_Variable a = Pop();
                                    sws_Variable b = Pop();

                                    switch (a.Type) {
                                        case TYPE_NULL: {
                                                if (b.Value != null) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_BOOL: {
                                                if (TypeCheck(b, TYPE_BOOL, true)) {
                                                    if (a.Value != null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if ((bool)a.Value != (bool)b.Value) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_DOUBLE: {
                                                if (TypeCheck(b, TYPE_DOUBLE, true)) {
                                                    if (a.Value != null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if ((double)a.Value != (double)b.Value) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_STRING: {
                                                if (TypeCheck(b, TYPE_STRING, true)) {
                                                    if (a.Value != null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if (a.Value.ToString() != b.Value.ToString()) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_TABLE: {
                                                if (TypeCheck(b, TYPE_TABLE, true)) {
                                                    if (a.Value != null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if (a.Value != b.Value) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                        case TYPE_CLOSURE: {
                                                if (TypeCheck(b, TYPE_CLOSURE, true)) {
                                                    if (a.Value != null) {
                                                        Frame.PC += data;
                                                    }
                                                    break;
                                                }
                                                if (a.Value != b.Value) {
                                                    Frame.PC += data;
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case (int)op_jgt: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    if (a > b) {
                                        Frame.PC += data;
                                    }
                                    break;
                                }
                            case (int)op_jgte: {
                                    double a = (double)PopTypeCheck(TYPE_DOUBLE);
                                    double b = (double)PopTypeCheck(TYPE_DOUBLE);

                                    if (a >= b) {
                                        Frame.PC += data;
                                    }
                                    break;
                                }
                            case (int)op_jfalse: {
                                    sws_Variable a = Pop();

                                    if (a.Type == TYPE_BOOL) {
                                        if (!(bool)a.Value) {
                                            Frame.PC += data;
                                        }
                                    } else if (a.Type == TYPE_NULL || a.Value == null) {
                                        Frame.PC += data;
                                    }
                                    break;
                                }
                            case (int)op_jtrue: {
                                    sws_Variable a = Pop();

                                    if (a.Type == TYPE_BOOL) {
                                        if ((bool)a.Value) {
                                            Frame.PC += data;
                                        }
                                    } else if (a.Type != TYPE_NULL && a.Value != null) {
                                        Frame.PC += data;
                                    }
                                    break;
                                }
                            case (int)op_jfnp: {
                                    sws_Variable a = Pop();

                                    if (a.Type == TYPE_BOOL) {
                                        if (!(bool)a.Value) {
                                            Frame.PC += data;
                                            Push(VAR_FALSE);
                                        }
                                    } else if (a.Type == TYPE_NULL || a.Value == null) {
                                        Frame.PC += data;
                                        Push(VAR_NULL);
                                    }
                                    break;
                                }
                            case (int)op_jtnp: {
                                    sws_Variable a = Pop();

                                    if (a.Type == TYPE_BOOL) {
                                        if ((bool)a.Value) {
                                            Frame.PC += data;
                                            Push(VAR_TRUE);
                                        }
                                    } else if (a.Type != TYPE_NULL && a.Value != null) {
                                        Frame.PC += data;
                                        Push(a);
                                    }
                                    break;
                                }
                            case (int)op_tablenew: {
                                    Push(new sws_Variable(new sws_Table(), TYPE_TABLE));
                                    break;
                                }
                            case (int)op_tableset: {
                                    int dataTemp = Math.Abs(data);

                                    sws_Table table = (sws_Table)PopTypeCheck(TYPE_TABLE);

                                    for (int i = 0; i < dataTemp; i++) {
                                        table.Set(Pop(), Pop());
                                    }

                                    if (negative) {
                                        Push(new sws_Variable(table, TYPE_TABLE));
                                    }
                                    break;
                                }
                            case (int)op_tableget: {
                                    sws_Variable index = Pop();

                                    sws_Table table = (sws_Table)PopTypeCheck(TYPE_TABLE);
                                    Push(table.Get(index));

                                    break;
                                }
                            case (int)op_tablekeys: {
                                    sws_Table t = (sws_Table)PopTypeCheck(TYPE_TABLE);
                                    Push(t.Pairs());
                                    break;
                                }
                            case (int)op_closure: {
                                    int dataTemp = Math.Abs(data);

                                    string closureName = Constants[dataTemp].Value.ToString();

                                    sws_Frame closure = new sws_Frame();

                                    closure.Name = closureName;

                                    closure.LuaCall = closureName.Contains("lua ");

                                    if (!closure.LuaCall) {
                                        closure.Copy(Frames[closureName]);

                                        if (closure.UpvalueIndexes.Length > 0) {
                                            CallStack[CSP].Copy(Frame);

                                            for (int i = CSP; i >= 0; i--) {
                                                if (Array.IndexOf(closure.UpvalueFrames, CallStack[i].Name) != -1) {
                                                    for (int j = 0; j < closure.UpvalueFrames.Length; j++) {
                                                        if (closure.UpvalueFrames[j] == CallStack[i].Name) {
                                                            closure.Upvalues[j] = CallStack[i].Locals[closure.UpvalueIndexes[j]].Clone();
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    Push(new sws_Variable(closure, TYPE_CLOSURE));

                                    break;
                                }
                            case (int)op_call: {
                                    int dataTemp = Math.Abs(data);

                                    sws_Frame closure = new sws_Frame();

                                    sws_Frame closureTemp = (sws_Frame)Pop().Value;

                                    if (closureTemp.LuaCall) {
                                        throw new sws_VMError($"Unable to call lua function '{closureTemp.Name}' in C# VM. (No lua functions can be called).", Prg, Frame.Name, Frame.PC - 1);
                                    }

                                    closure.Copy(closureTemp);

                                    closure.ReturnValues = !negative;

                                    for (int i = 0; i < dataTemp; i++) {
                                        closure.Locals[i] = Pop();
                                    }

                                    if (CSP >= CALLSTACK_SIZE) {
                                        throw new sws_Error($"Exceeded max call stack size of {CALLSTACK_SIZE}.");
                                    }

                                    CallStack[CSP++].Copy(Frame);

                                    for (int i = CSP - 1; i >= 0; i--) {
                                        if (Array.IndexOf(closure.UpvalueFrames, CallStack[i].Name) != -1) {
                                            if (!closure.UpvalueCSIndexes.ContainsKey(CallStack[i].Name)) {
                                                closure.UpvalueCSIndexes.Add(CallStack[i].Name, i);
                                            }
                                        }
                                    }

                                    Frame.Copy(closure);
                                    break;
                                }
                            case (int)op_return: {
                                    CSP--;
                                    if (CSP == 1 && (hasOnTickFunc || hasOnDrawFunc) && name != string.Empty) {
                                        halt = true;
                                        break;
                                    }

                                    if (!Frame.ReturnValues) {
                                        SP -= data;
                                    }

                                    Frame.Copy(CallStack[CSP]);

                                    break;
                                }
                            case (int)op_halt: {
                                    halt = true;
                                    break;
                                }
                            case (int)op_print: {
                                    if (data == 0) {
                                        Console.WriteLine(Pop().Value ?? "null");
                                    } else if (data == 1) {
                                        Console.Write(Pop().Value ?? "null");
                                    }
                                    break;
                                }
                            case (int)op_type: {
                                    sws_Variable a = Pop();

                                    if (a.Type == TYPE_NULL || a.Value == null) {
                                        Push(new sws_Variable("nil", TYPE_STRING));
                                    } else if (a.Type == TYPE_BOOL) {
                                        Push(new sws_Variable("boolean", TYPE_STRING));
                                    } else if (a.Type == TYPE_DOUBLE) {
                                        Push(new sws_Variable("number", TYPE_STRING));
                                    } else if (a.Type == TYPE_STRING) {
                                        Push(new sws_Variable("string", TYPE_STRING));
                                    } else if (a.Type == TYPE_TABLE) {
                                        Push(new sws_Variable("table", TYPE_STRING));
                                    } else if (a.Type == TYPE_CLOSURE) {
                                        Push(new sws_Variable("table", TYPE_STRING));
                                    }

                                    break;
                                }
                        }

                        if (halt) {
                            break;
                        }
                    }

                    //stop executing
                    if (hasOnTickFunc || hasOnDrawFunc) {
                        //next function to run is going to be onTick or onDraw, this accounts for them being pushed onto the stack
                        CSP = 2;
                    } else {
                        return;
                    }

                    last = name;
                }
            } catch (Exception e) {
                if (e.GetType().Name == "sws_VMError") {
                    return;
                }

                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                    
                new sws_VMError("Exception.", Prg, Frame.Name, Frame.PC - 1);
            }
        }

        private void Push(sws_Variable variable) {
            if (SP >= STACK_SIZE) {
                throw new sws_VMError($"Stack size of {STACK_SIZE} exceeded.", Prg, Frame.Name, Frame.PC - 1);
            }

            Stack[SP++] = variable;
        }

        private sws_Variable Pop() {
            if (SP == 0) { //assumming the bytecode is generated and executed correctly, this can't happen
                throw new sws_VMError($"Stack underflow.", Prg, Frame.Name, Frame.PC - 1);
            }

            return Stack[--SP];
        }

        private object PopTypeCheck(int type) {
            sws_Variable variable = Pop();
            
            if (variable.Type != type) {
                throw new sws_VMError($"Expected type '{TypeToString(type)}', got type '{TypeToString(variable.Type)}'.", Prg, Frame.Name, Frame.PC - 1);
            }

            return variable.Value;
        }

        private long PopInt64() {
            double value = (double)PopTypeCheck(TYPE_DOUBLE);

            if (value % 1 != 0) {
                throw new sws_VMError($"Expected double with no decimal, instead got {value}.", Prg, Frame.Name, Frame.PC - 1);
            }

            return (long)value;
        }

        private bool TypeCheck(sws_Variable variable, int type, bool ignoreNull = false) {
            if (variable.Type != type) {
                if(ignoreNull && variable.Type == TYPE_NULL) {
                    return variable.Type == TYPE_NULL || variable.Value == null;
                }
                throw new sws_VMError($"Expected type '{TypeToString(type)}', got type '{TypeToString(variable.Type)}'.", Prg, Frame.Name, Frame.PC - 1);
            }
            return variable.Type == TYPE_NULL || variable.Value == null;
        }

        private string TypeToString(int type) {
            switch(type) {
                case TYPE_NULL: return "null";
                case TYPE_BOOL: return "bool";
                case TYPE_DOUBLE: return "double";
                case TYPE_STRING: return "string";
                case TYPE_TABLE: return "table";
                case TYPE_CLOSURE: return "closure";
                default: throw new sws_VMError($"Unknown variable type ({type}).", Prg, Frame.Name, Frame.PC - 1);
            }
        }
    }
}
