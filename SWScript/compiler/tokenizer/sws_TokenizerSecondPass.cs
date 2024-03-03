using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWScript.compiler.sws_Compiler;
using static SWScript.compiler.sws_TokenType;

namespace SWScript.compiler {
    internal class sws_TokenizerSecondPass {
        private static int Line = 1;
        private static int Column = 0;
        private static int Current = 0;

        public static void Tokenize() {
            Line = 1;
            Column = 0;
            Current = 0;

            Tokens = new List<sws_Token>();

            while (!AtEnd()) {
                NextToken();
            }
        }

        private static void NextToken() {
            sws_Token t = Next();

            Line = t.NLine;
            Column = t.NColumn;

            switch (t.TokenType) {
                case punctuation_number_sign:
                    AddToken(operator_length);
                    break;
                case punctuation_greater_than:
                    if (Peek().TokenType == punctuation_greater_than) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_bitshiftright_assign);
                            Current += 2;
                        } else {
                            AddToken(operator_bitshiftright);
                            Current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_gte);
                        Current++;
                    } else {
                        AddToken(operator_gt);
                    }
                    break;
                case punctuation_less_than:
                    if (Peek().TokenType == punctuation_less_than) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_bitshiftleft_assign);
                            Current += 2;
                        } else {
                            AddToken(operator_bitshiftleft);
                            Current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_lte);
                        Current++;
                    } else {
                        AddToken(operator_lt);
                    }
                    break;
                case punctuation_equals_sign:
                    if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_eq);
                        Current++;
                    } else {
                        AddToken(operator_assign);
                    }
                    break;
                case punctuation_plus:
                    if (Peek().TokenType == punctuation_plus) {
                        AddToken(punctuation_doubleplus);
                        Current++;
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_add_assign);
                        Current++;
                    } else {
                        AddToken(operator_add);
                    }
                    break;
                case punctuation_minus:
                    //negative numbers
                    if (Peek().TokenType == punctuation_minus) {
                        AddToken(punctuation_doubleminus);
                        Current++;
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_sub_assign);
                        Current++;
                    } else if (Array.IndexOf(new sws_TokenType[] { identifier, literal_number, punctuation_parenthesis_closed, punctuation_brackets_closed }, Last().TokenType) != -1 || Array.IndexOf(new sws_TokenType[] { punctuation_doubleplus, punctuation_doubleminus }, Tokens.Last().TokenType) != -1) { //checks if previous token is something that could be subtracted from
                        AddToken(operator_sub);
                    } else {
                        AddToken(operator_minus);
                    }
                    break;
                case punctuation_asterisk:
                    if (Peek().TokenType == punctuation_asterisk) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_pow_assign);
                            Current += 2;
                        } else {
                            AddToken(operator_pow);
                            Current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_mul_assign);
                        Current++;
                    } else {
                        AddToken(operator_mul);
                    }
                    break;
                case punctuation_slash:
                    if(Peek().TokenType == punctuation_slash) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_floordiv_assign);
                            Current += 2;
                        } else {
                            AddToken(operator_floordiv);
                            Current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_div_assign);
                        Current++;
                    } else {
                        AddToken(operator_div);
                    }
                    break;
                case punctuation_percent_sign:
                    if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_mod_assign);
                        Current++;
                    } else {
                        AddToken(operator_mod);
                    }
                    break;
                case punctuation_ampersand:
                    if (Peek().TokenType == punctuation_ampersand) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_booland_assign);
                            Current += 2;
                        } else {
                            AddToken(operator_booland);
                            Current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_bitand_assign);
                        Current++;
                    } else {
                        AddToken(operator_bitand);
                    }
                    break;
                case punctuation_caret:
                    if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_bitxor_assign);
                        Current++;
                    } else {
                        AddToken(operator_bitxor);
                    }
                    break;
                case punctuation_tilde:
                    AddToken(operator_bitnot);
                    break;
                case punctuation_vertical_bar:
                    if (Peek().TokenType == punctuation_vertical_bar) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_boolor_assign);
                            Current += 2;
                        } else {
                            AddToken(operator_boolor);
                            Current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_bitor_assign);
                        Current++;
                    } else {
                        AddToken(operator_bitor);
                    }
                    break;
                case punctuation_exclamation_mark:
                    if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_neq);
                        Current++;
                    } else {
                        AddToken(operator_boolnot);
                    }
                    break;
                case punctuation_dollar_sign:
                    AddToken(operator_concat);
                    break;
                case keyword_else:
                    if (Peek().TokenType == keyword_if) {
                        AddToken(keyword_else_if);
                        Current++;
                    } else {
                        AddToken(keyword_else);
                    }
                    break;
                default:
                    Tokens.Add(t);
                    break;
            }
        }

        private static void AddToken(sws_TokenType tokenType) {
            Tokens.Add(new sws_Token(tokenType, Line, Column));
        }

        private static sws_Token Next() {
            return FirstPassTokens[Current++];
        }

        private static sws_Token Last() {
            if (Current - 2 < 0) {
                return sws_Token.Error();
            }
            return FirstPassTokens[Current - 2];
        }

        private static sws_Token Peek() {
            if (AtEnd()) {
                return sws_Token.Error();
            }
            return FirstPassTokens[Current];
        }

        private static sws_Token Peek2() {
            if (Current + 1 >= FirstPassTokens.Count) {
                return sws_Token.Error();
            }
            return FirstPassTokens[Current + 1];
        }

        private static bool AtEnd() {
            return Current >= FirstPassTokens.Count;
        }
    }
}
