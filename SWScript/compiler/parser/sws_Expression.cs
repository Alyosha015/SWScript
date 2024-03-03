using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWScript.compiler.sws_TokenType;

namespace SWScript.compiler {
    internal enum sws_ExpressionType {
        Unary, Binary, Literal, Variable, Table, TableGet, LuaCall, ClosureCall, Closure
    }

    internal class sws_Expression {
        internal class sws_Expression_Literal {
            public object Value;
            public sws_DataType Type;

            public sws_Expression_Literal(object value, sws_DataType type) {
                Value = value;
                Type = type;
            }
        }

        public sws_ExpressionType Type;

        public object Value;

        public sws_DataType ValueType;

        public string Name;

        /// <summary>
        /// If closure is for a lua call (ex. 'local gn = lua.input.getNumber').
        /// </summary>
        public bool LuaClosure;

        public sws_TokenType Op;
        public sws_Expression Right;
        public sws_Expression Left;

        /// <summary>
        /// Indicies of elements in table decleration.
        /// </summary>
        public List<sws_Expression> ElementIndices;

        /// <summary>
        /// Elements in table decleration.
        /// </summary>
        public List<sws_Expression> Elements;

        /// <summary>
        /// Indices for table access.
        /// </summary>
        public List<sws_Expression> Indices;

        /// <summary>
        /// Store arguments for function call.
        /// </summary>
        public List<sws_Expression> Arguments;

        /// <summary>
        /// Parent object of sws_Expression
        /// </summary>
        public sws_Expression Parent;

        public sws_Expression Unary(sws_TokenType op, sws_Expression right) {
            right.Parent = this;

            //expressions which are a minus operator on a literal number as simplified as a negative literal number
            if (op == operator_minus && right.Type == sws_ExpressionType.Literal) {
                Type = sws_ExpressionType.Literal;

                Value = -(double)right.Value;
                ValueType = right.ValueType;

                return this;
            }

            Type = sws_ExpressionType.Unary;

            Op = op;
            Right = right;

            return this;
        }

        public sws_Expression Binary(sws_Expression left, sws_TokenType op, sws_Expression right) {
            Type = sws_ExpressionType.Binary;

            left.Parent = this;
            right.Parent = this;

            Left = left;

            Op = op;
            
            Right = right;

            return this;
        }

        public sws_Expression Literal(object value, sws_DataType valueType) {
            Type = sws_ExpressionType.Literal;

            Value = value;
            ValueType = valueType;

            return this;
        }

        public sws_Expression Literal(sws_Token token) {
            Type = sws_ExpressionType.Literal;
            Value = token.Literal;

            switch(token.TokenType) {
                case keyword_null:
                    Value = null;
                    ValueType = sws_DataType.Null;
                    break;
                case keyword_true:
                    Value = true;
                    ValueType = sws_DataType.Bool;
                    break;
                case keyword_false:
                    Value = false;
                    ValueType = sws_DataType.Bool;
                    break;
                case literal_number:
                    ValueType = sws_DataType.Double;
                    break;
                case literal_string:
                    ValueType = sws_DataType.String;
                    break;
                case identifier:
                    ValueType = sws_DataType.String;
                    break;
            }

            return this;
        }

        public sws_Expression Variable(string name) {
            Type = sws_ExpressionType.Variable;

            Name = name;

            return this;
        }

        public sws_Expression Table(List<sws_Expression> elementIndices, List<sws_Expression> elements) {
            Type = sws_ExpressionType.Table;

            ElementIndices = elementIndices;
            Elements = elements;

            return this;
        }

        public sws_Expression TableGet(sws_Expression table, List<sws_Expression> indices) {
            Type = sws_ExpressionType.TableGet;

            Name = null;

            if (table.Type == sws_ExpressionType.Variable) {
                Name = table.Name;
            }

            Right = table;

            Indices = indices;

            return this;
        }

        public sws_Expression LuaCall(string name, List<sws_Expression> arguments) {
            Type = sws_ExpressionType.LuaCall;

            Name = name;
            Arguments = arguments;

            return this;
        }

        public sws_Expression ClosureCall(sws_Expression left, List<sws_Expression> arguments) {
            Type = sws_ExpressionType.ClosureCall;

            Left = left;
            left.Parent = this;

            Arguments = arguments;

            return this;
        }

        public sws_Expression Closure(string name, bool luaClosure=false) {
            Type = sws_ExpressionType.Closure;
            
            Name = name;
            LuaClosure = luaClosure;

            return this;
        }

        public sws_Expression ConstantFold() {
            if(!PossibleToConstantFold()) {
                return this;
            }
            
            try {
                sws_Expression_Literal result = ConstantFold(this);
                
                Type = sws_ExpressionType.Literal;
                Value = result.Value;
                ValueType = result.Type;

                return new sws_Expression().Literal(result.Value, result.Type);
            } catch (sws_Error) {
                return this;
            }

            sws_Expression_Literal ConstantFold(sws_Expression expr) {
                if (expr.Type == sws_ExpressionType.Unary) {
                    sws_Expression_Literal right = ConstantFold(expr.Right);

                    sws_Expression_Literal output = new sws_Expression_Literal(0d, sws_DataType.Double);

                    switch (expr.Op) {
                        case operator_length: {
                                string a = CheckIsString(right);

                                output.Value = a.Length;
                                break;
                            }
                        case operator_boolnot: {
                                bool a = CheckIsBool(right);

                                output.Type = sws_DataType.Bool;

                                output.Value = !a;
                                break;
                            }
                        case operator_bitnot: {
                                long a = CheckIsInt64(right);

                                output.Value = (double)~a;
                                break;
                            }
                        case operator_minus: {
                                double a = CheckIsDouble(right);

                                output.Value = -a;
                                break;
                            }
                        default: {
                                throw new sws_Error($"[CONSTANT FOLD] Unable to perform operation '{expr.Op}' on literal values.");
                            }
                    }

                    return output;
                } else if (expr.Type == sws_ExpressionType.Binary) {
                    sws_Expression_Literal right = ConstantFold(expr.Right);
                    sws_Expression_Literal left = ConstantFold(expr.Left);

                    sws_Expression_Literal output = new sws_Expression_Literal(0d, sws_DataType.Double);

                    switch (expr.Op) {
                        case operator_add: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Value = a + b;
                                break;
                            }
                        case operator_sub: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Value = a - b;
                                break;
                            }
                        case operator_mul: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Value = a * b;
                                break;
                            }
                        case operator_div: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Value = a / b;
                                break;
                            }
                        case operator_floordiv: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Value = (double)((long)(a / b) - Convert.ToInt32(((a < 0) ^ (b < 0)) && (a % b != 0)));
                                break;
                            }
                        case operator_pow: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Value = Math.Pow(a, b);
                                break;
                            }
                        case operator_mod: {
                                double dividend = CheckIsDouble(left);
                                double divisor = CheckIsDouble(right);

                                //copy behavior of lua % operator
                                double result = dividend % divisor;
                                if (result < 0 && divisor > 0 || result > 0 && divisor < 0) {
                                    result += divisor;
                                }

                                output.Value = result;
                                break;
                            }
                        case operator_booland: {
                                bool a = CheckIsBool(left);
                                bool b = CheckIsBool(right);

                                output.Type = sws_DataType.Bool;

                                output.Value = a && b;
                                break;
                            }
                        case operator_boolor: {
                                bool a = CheckIsBool(left);
                                bool b = CheckIsBool(right);

                                output.Type = sws_DataType.Bool;

                                output.Value = a || b;
                                break;
                            }
                        case operator_bitand: {
                                long a = CheckIsInt64(left);
                                long b = CheckIsInt64(right);

                                output.Value = (double)(a & b);
                                break;
                            }
                        case operator_bitxor: {
                                long a = CheckIsInt64(left);
                                long b = CheckIsInt64(right);

                                output.Value = (double)(a ^ b);
                                break;
                            }
                        case operator_bitor: {
                                long a = CheckIsInt64(left);
                                long b = CheckIsInt64(right);

                                output.Value = (double)(a | b);
                                break;
                            }
                        case operator_bitshiftleft: {
                                long a = CheckIsInt64(left);
                                long b = CheckIsInt64(right);

                                output.Value = (double)(a << (int)b);
                                break;
                            }
                        case operator_bitshiftright: {
                                long a = CheckIsInt64(left);
                                long b = CheckIsInt64(right);

                                output.Value = (double)(a >> (int)b);
                                break;
                            }
                        case operator_concat: {
                                string a = left.ToString();
                                string b = right.ToString();

                                output.Type = sws_DataType.String;

                                output.Value = a + b;
                                break;
                            }
                        case operator_eq: {
                                output.Type = sws_DataType.Bool;

                                switch (left.Type) {
                                    case sws_DataType.Null: {
                                            output.Value = left.Value == right.Value;
                                            break;
                                        }
                                    case sws_DataType.Bool: {
                                            bool a = CheckIsBool(left);
                                            bool b = CheckIsBool(right);

                                            output.Value = a == b;
                                            break;
                                        }
                                    case sws_DataType.Double: {
                                            double a = CheckIsDouble(left);
                                            double b = CheckIsDouble(right);

                                            output.Value = a == b;
                                            break;
                                        }
                                    case sws_DataType.String: {
                                            string a = CheckIsString(left);
                                            string b = CheckIsString(right);

                                            output.Value = a == b;
                                            break;
                                        }
                                }
                                break;
                            }
                        case operator_neq: {
                                output.Type = sws_DataType.Bool;

                                switch (left.Type) {
                                    case sws_DataType.Null: {
                                            output.Value = left.Value != right.Value;
                                            break;
                                        }
                                    case sws_DataType.Bool: {
                                            bool a = CheckIsBool(left);
                                            bool b = CheckIsBool(right);

                                            output.Value = a != b;
                                            break;
                                        }
                                    case sws_DataType.Double: {
                                            double a = CheckIsDouble(left);
                                            double b = CheckIsDouble(right);

                                            output.Value = a != b;
                                            break;
                                        }
                                    case sws_DataType.String: {
                                            string a = CheckIsString(left);
                                            string b = CheckIsString(right);

                                            output.Value = a != b;
                                            break;
                                        }
                                }
                                break;
                            }
                        case operator_gt: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Type = sws_DataType.Bool;

                                output.Value = a > b;
                                break;
                            }
                        case operator_lt: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Type = sws_DataType.Bool;

                                output.Value = a < b;
                                break;
                            }
                        case operator_gte: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Type = sws_DataType.Bool;

                                output.Value = a >= b;
                                break;
                            }
                        case operator_lte: {
                                double a = CheckIsDouble(left);
                                double b = CheckIsDouble(right);

                                output.Type = sws_DataType.Bool;

                                output.Value = a <= b;
                                break;
                            }
                        default: {
                                throw new sws_Error($"[CONSTANT FOLD] Unable to perform operation '{expr.Op}' on literal values.");
                            }
                    }

                    return output;
                } else if (expr.Type == sws_ExpressionType.Literal) {
                    return new sws_Expression_Literal(expr.Value, expr.ValueType);
                }

                //this is here to calm down the compiler. One of the above if statements always runs.
                return null;

                double CheckIsDouble(sws_Expression_Literal value) {
                    if (value.Type == sws_DataType.Double) {
                        return (double)value.Value;
                    }

                    throw new sws_Error($"[CONSTANT FOLD] Expected '{value.Value}' to be a Double, instead got '{value.Type}'.");
                }

                long CheckIsInt64(sws_Expression_Literal value) {
                    if (value.Type == sws_DataType.Double && (double)value.Value % 1 == 0) {
                        return Convert.ToInt64(value.Value);
                    }

                    throw new sws_Error($"[CONSTANT FOLD] Expected '{value.Value}' to be an Integer (no decimal point).");
                }

                bool CheckIsBool(sws_Expression_Literal value) {
                    if (value.Type == sws_DataType.Bool) {
                        return (bool)value.Value;
                    }

                    throw new sws_Error($"[CONSTANT FOLD] Expected '{value.Value}' to be a Bool, instead got '{value.Type}'.");
                }

                string CheckIsString(sws_Expression_Literal value) {
                    if (value.Type == sws_DataType.String) {
                        return value.Value.ToString();
                    }

                    throw new sws_Error($"[CONSTANT FOLD] Expected '{value.Value}' to be a String, instead got '{value.Type}'.");
                }
            }
        }

        public bool PossibleToConstantFold() {
            return IsPossibleToConstantFoldRecursive(this) && Type != sws_ExpressionType.Literal;

            bool IsPossibleToConstantFoldRecursive(sws_Expression expr) {
                if (expr.Type == sws_ExpressionType.Unary) {
                    return IsPossibleToConstantFoldRecursive(expr.Right);
                } else if (expr.Type == sws_ExpressionType.Binary) {
                    return IsPossibleToConstantFoldRecursive(expr.Right) && IsPossibleToConstantFoldRecursive(expr.Left);
                } else if (expr.Type != sws_ExpressionType.Literal) {
                    return false;
                }
                
                return true;
            }
        }

        public void Print() {
            Console.WriteLine(ToString());
        }

        public override string ToString() {
            switch (Type) {
                case sws_ExpressionType.Literal:
                    return Value.ToString();
                case sws_ExpressionType.Variable:
                    return Name;
                case sws_ExpressionType.Table: {
                        string str = "{";
                        for (int i = 0; i < Elements.Count; i++) {
                            str += "[" + ElementIndices[i].ToString() + "]=" + Elements[i].ToString() + (i + 1 < Elements.Count ? ", " : string.Empty);
                        }
                        return str + "}";
                    }
                case sws_ExpressionType.TableGet: {
                        string str = Right.ToString();
                        foreach (sws_Expression x in Indices) {
                            str += "[" + x.ToString() + "]";
                        }
                        return str;
                    }
                case sws_ExpressionType.LuaCall: {
                        string str = Name.Replace(' ', '.') + "(";
                        for (int i = 0; i < Arguments.Count; i++) {
                            str += Arguments[i].ToString() + (i + 1 != Arguments.Count ? ", " : ")");
                        }
                        if (Arguments.Count == 0) {
                            str += ")";
                        }
                        return str;
                    }
                case sws_ExpressionType.ClosureCall: {
                        string str = Left.ToString() + "(";
                        for (int i = 0; i < Arguments.Count; i++) {
                            str += Arguments[i].ToString() + (i + 1 != Arguments.Count ? ", " : ")");
                        }
                        if (Arguments.Count == 0) {
                            str += ")";
                        }
                        return str;
                    }
                case sws_ExpressionType.Closure: {
                        return (LuaClosure ? "lua." : string.Empty ) + Name.Replace(' ', '.');
                    }
                case sws_ExpressionType.Binary: {
                        return $"({Left} {Op} {Right})";
                    }
                case sws_ExpressionType.Unary: {
                        return $"{Op}({Right})";
                    }
                default:
                    return string.Empty;
            }
        }
    }
}
