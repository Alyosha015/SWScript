using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWS.Compiler {
    public enum sws_DataType {
        Null,
        Bool,
        Double,
        String,
        Table,
        Function,
    }

    public enum sws_VariableType {
        Constant,
        Local,
        Upvalue,
        Global,
    }

    public class sws_Variable {
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

        public string TableToStr() {
            StringBuilder output = new StringBuilder();

            output.Append("{");

            sws_Table table = (sws_Table)Value;

            sws_Variable[] keys = table.Table.Keys.ToArray();

            for (int j = 0; j < keys.Length; j++) {
                sws_Variable key = keys[j];
                sws_Variable value = table.Table[key];

                if (value.ValueType == sws_DataType.Table) {
                    throw new sws_ParserError("Attempted to encode nested table. (Compiler bug)");
                }

                output.Append(key.Value.ToString() ?? "null");
                output.Append('=');
                output.Append(value.Value.ToString() ?? "null");
                if (j + 1 < keys.Length) {
                    output.Append(", ");

                }
            }
            
            output.Append("}");
            return output.ToString();
        }

        /// <summary>
        /// Converts value to string. Used for constant variable type.
        /// </summary>
        /// <returns></returns>
        public string ToConstString(char M1) {
            StringBuilder output = new StringBuilder();
            output.Append((int)ValueType);
            output.Append(M1);

            switch (ValueType) {
                case sws_DataType.Null: output.Append(" " + M1); break;
                case sws_DataType.Bool: output.Append((bool)Value ? "SWS" + M1 : " " + M1); break;
                case sws_DataType.Double: output.Append(Value.ToString() + M1); break;
                case sws_DataType.String: output.Append(Value.ToString() + M1); break;
                case sws_DataType.Table: {
                        sws_Table table = (sws_Table)Value;

                        sws_Variable[] keys = table.Table.Keys.ToArray();

                        output.Append(keys.Length.ToString());
                        output.Append(M1);

                        foreach (sws_Variable key in keys) {
                            sws_Variable value = table.Table[key];

                            if (value.ValueType == sws_DataType.Table) {
                                throw new sws_ParserError("Attempted to encode nested table. (Compiler bug)");
                            }

                            output.Append(key.ToConstString(M1));
                            output.Append(value.ToConstString(M1));
                        }

                        break;
                    }
                default: throw new sws_ParserError($"Attempted to encode const type '{ValueType}' to string. (Compiler bug)");
            }

            return output.ToString();
        }
    }
}
