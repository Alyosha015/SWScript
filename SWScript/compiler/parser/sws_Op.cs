using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWS.Compiler.sws_Opcode;

namespace SWS.Compiler {
    /// <summary>
    /// Opcodes used by SWScript bytecode. These are arranged in groups for clarity. However the actual number codes for the opcodes are grouped differently, they are loosely based on number of arguments needed (for example 0-20 all need to pop two values off the stack). This is done because it lets me make the lua interpreter use less characters.
    /// 
    ///  0-21 pop 2 values. 0-17 also push 1 value.
    /// 22-37 pop 1 value.  32-45 also push 1 value.
    /// 38-49 don't pop values.
    /// 50-52 vary.
    /// 
    /// </summary>
    public enum sws_Opcode {
        op_getconst = 38,
        op_getlocal = 39,
        op_getupval = 40,
        op_getglobal = 41,
        op_setlocal = 22,
        op_setupval = 23,
        op_setglobal = 24,
        op_dup = 25,

        op_loadimm = 42,
        op_loadbool = 43,
        op_loadnull = 44,

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

        op_addimm = 46, //special case to pop no value rule, instead modify value on stack
        op_minus = 32,
        op_boolnot = 33,
        op_bitnot = 34,
        op_len = 35,

        op_concat = 12,

        op_eq = 13,
        op_neq = 14,
        op_lt = 15,
        op_lte = 16,

        op_jmp = 47,
        op_jindexed = 31,
        op_jeq = 18,
        op_jneq = 19,
        op_jgt = 20,
        op_jgte = 21,
        op_jfalse = 26,
        op_jtrue = 27,
        op_jfnp = 28,
        op_jtnp = 29,

        op_tablenew = 45,
        op_tableset = 50,
        op_tableget = 17,
        op_tablekeys = 36,

        op_closure = 48,
        op_call = 51,
        op_return = 52,

        op_halt = 49,
        op_print = 30,
        op_type = 37,
    }
    
    public class sws_Op {
        public sws_Opcode Opcode;
        public int Data;

        public int Line;

        public sws_Op(sws_Opcode opcode, int data, int line) {
            Opcode = opcode;
            Data = data;
            Line = line;
        }

        public bool IsJumpOp() {
            return Opcode == op_jmp || Opcode == op_jindexed || Opcode == op_jeq || Opcode == op_jneq || Opcode == op_jgt || Opcode == op_jgte || Opcode == op_jfalse || Opcode == op_jtrue || Opcode == op_jfnp || Opcode == op_jtnp;
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
