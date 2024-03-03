using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWScript.compiler.sws_Opcode;

namespace SWScript.compiler {
    /// <summary>
    /// Opcodes used by SWScript bytecode. These are arranged in groups for clarity. However the actual number codes for the opcodes are grouped differently, they are loosely based on number of arguments needed (for example 0-20 all need to pop two values off the stack). This is done because it lets me make the lua interpreter use less characters.
    /// 
    ///  0-21 pop 2 values. 0-17 also push 1 value.
    /// 22-36 pop 1 value.  31-44 also push 1 value.
    /// 37-48 don't pop values.
    /// 49-51 vary.
    /// 
    /// </summary>
    internal enum sws_Opcode {
        op_getconst = 37,
        op_getlocal = 38,
        op_getupval = 39,
        op_getglobal = 40,
        op_setlocal = 22,
        op_setupval = 23,
        op_setglobal = 24,
        op_dup = 25,

        op_loadimm = 41,
        op_loadbool = 42,
        op_loadnull = 43,

        op_add = 0,
        op_sub = 1,
        op_mul = 2,
        op_div = 3,
        op_floordiv = 4,
        op_pow = 5,
        op_mod = 6,
        op_bitand = 7,
        op_bitor = 8,
        op_bitxor = 9,
        op_bitshiftleft = 10,
        op_bitshiftright = 11,

        op_addimm = 45, //special case to pop no value rule, instead modify value on stack
        op_minus = 31,
        op_boolnot = 32,
        op_bitnot = 33,
        op_len = 34,

        op_concat = 12,

        op_eq = 13,
        op_neq = 14,
        op_lt = 15,
        op_lte = 16,

        op_jmp = 46,
        op_jeq = 18,
        op_jneq = 19,
        op_jgt = 20,
        op_jgte = 21,
        op_jfalse = 26,
        op_jtrue = 27,
        op_jfnp = 28,
        op_jtnp = 29,

        op_tablenew = 44,
        op_tableset = 49,
        op_tableget = 17,
        op_tablekeys = 35,

        op_closure = 47,
        op_call = 50,
        op_return = 51,

        op_halt = 48,
        op_print = 30,
        op_type = 36,
    }
    
    internal class sws_Op {
        public sws_Opcode Opcode;
        public int Data;

        public int Line;

        public sws_Op(sws_Opcode opcode, int data) {
            Opcode = opcode;
            Data = data;
            Line = sws_Parser.LastToken.NLine;
        }

        public bool IsJumpOp() {
            return Opcode == op_jmp || Opcode == op_jeq || Opcode == op_jneq || Opcode == op_jgt || Opcode == op_jgte || Opcode == op_jfalse || Opcode == op_jtrue || Opcode == op_jfnp || Opcode == op_jtnp;
        }

        public bool IsDataOp() { //excludes op_loadxxxx instructions
            return Opcode == op_getconst || Opcode == op_getlocal || Opcode == op_getupval || Opcode == op_getglobal || Opcode == op_setlocal || Opcode == op_setupval || Opcode == op_setglobal;
        }

        public int ExportToInt() {
            int tempData = Data;

            //remove placeholder value
            if (tempData == int.MinValue) {
                tempData = 0;
            }

            //force to 17 bit number
            int data = Math.Abs(tempData) & 0b1_1111_1111_1111_1111;

            //add sign bit
            if (tempData < 0) {
                data |= 0b10_0000_0000_0000_0000;
            }

            data += ((int)Opcode) << 18;

            return data;
        }
    }
}
