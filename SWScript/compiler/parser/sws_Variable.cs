using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScript.compiler {
    internal enum sws_DataType {
        Null,
        Bool,
        Double,
        String,
        Table,
        Function,
    }

    internal enum sws_VariableType {
        Constant,
        Local,
        Upvalue,
        Global,
    }

    internal class sws_Variable {
        public string Name;
        public sws_VariableType VariableType;
        /// <summary>
        /// Index of variable in constant/local/upvalue/global array.
        /// </summary>
        public int Index;

        public object Value;
        public sws_DataType ValueType;

        //the three values below are only used by locals
        /// <summary>
        /// Index of bytecode where scope of a local starts.
        /// </summary>
        public int ScopeStart;

        /// <summary>
        /// Index of bytecode where scope of a local ends.
        /// </summary>
        public int ScopeEnd;

        /// <summary>
        /// Used to set variable scope.
        /// </summary>
        public int Depth;

        //the two values below are only used by upvalues
        /// <summary>
        /// Stack frame local referenced as upvalue is in.
        /// </summary>
        public string StackFrameReference;

        /// <summary>
        /// Index of local referenced as upvalue.
        /// </summary>
        public int LocalIndexReference;

        public sws_Variable Constant(object value, sws_DataType valueType, int index) {
            VariableType = sws_VariableType.Constant;

            Value = value;
            ValueType = valueType;
            Index = index;

            return this;
        }

        public sws_Variable Local(string name, int index, int scopeStart, int depth) {
            VariableType = sws_VariableType.Local;

            Name = name;
            Index = index;
            ScopeStart = scopeStart;
            ScopeEnd = -1;
            Depth = depth;

            return this;
        }

        public sws_Variable Upvalue(string name, int index, string stackFrameReference, int localIndexReference) {
            VariableType = sws_VariableType.Upvalue;

            Name = name;
            Index = index;
            StackFrameReference = stackFrameReference;
            LocalIndexReference = localIndexReference;

            return this;
        }

        public sws_Variable Global(string name, int index) {
            VariableType = sws_VariableType.Global;

            Name = name;
            Index = index;

            return this;
        }
    }
}
