using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWS.Compiler.sws_TokenType;

namespace SWS.Compiler {
    internal class sws_TokenizerSecondPass {
        private List<sws_Token> _firstPassTokens;
        private List<sws_Token> _tokens;

        private int _line = 1;
        private int _column = 0;
        private int _current = 0;

        public List<sws_Token> Tokenize(List<sws_Token> firstPassTokens) {
            _firstPassTokens = firstPassTokens;
            _tokens = new List<sws_Token>();

            _line = 1;
            _column = 0;
            _current = 0;

            while (!AtEnd()) {
                NextToken();
            }

            return _tokens;
        }

        private void NextToken() {
            sws_Token t = Next();

            _line = t.NLine;
            _column = t.NColumn;

            switch (t.TokenType) {
                case punctuation_number_sign:
                    AddToken(operator_length);
                    break;
                case punctuation_greater_than:
                    if (Peek().TokenType == punctuation_greater_than) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_bitshiftright_assign);
                            _current += 2;
                        } else {
                            AddToken(operator_bitshiftright);
                            _current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_gte);
                        _current++;
                    } else {
                        AddToken(operator_gt);
                    }
                    break;
                case punctuation_less_than:
                    if (Peek().TokenType == punctuation_less_than) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_bitshiftleft_assign);
                            _current += 2;
                        } else {
                            AddToken(operator_bitshiftleft);
                            _current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_lte);
                        _current++;
                    } else {
                        AddToken(operator_lt);
                    }
                    break;
                case punctuation_equals_sign:
                    if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_eq);
                        _current++;
                    } else {
                        AddToken(operator_assign);
                    }
                    break;
                case punctuation_plus:
                    if (Peek().TokenType == punctuation_plus) {
                        AddToken(punctuation_doubleplus);
                        _current++;
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_add_assign);
                        _current++;
                    } else {
                        AddToken(operator_add);
                    }
                    break;
                case punctuation_minus:
                    //negative numbers
                    if (Peek().TokenType == punctuation_minus) {
                        AddToken(punctuation_doubleminus);
                        _current++;
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_sub_assign);
                        _current++;
                    } else if (Array.IndexOf(new sws_TokenType[] { identifier, literal_number, punctuation_parenthesis_closed, punctuation_brackets_closed }, Last().TokenType) != -1 || Array.IndexOf(new sws_TokenType[] { punctuation_doubleplus, punctuation_doubleminus }, _tokens.Last().TokenType) != -1) { //checks if previous token is something that could be subtracted from
                        AddToken(operator_sub);
                    } else {
                        AddToken(operator_minus);
                    }
                    break;
                case punctuation_asterisk:
                    if (Peek().TokenType == punctuation_asterisk) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_pow_assign);
                            _current += 2;
                        } else {
                            AddToken(operator_pow);
                            _current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_mul_assign);
                        _current++;
                    } else {
                        AddToken(operator_mul);
                    }
                    break;
                case punctuation_slash:
                    if(Peek().TokenType == punctuation_slash) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_floordiv_assign);
                            _current += 2;
                        } else {
                            AddToken(operator_floordiv);
                            _current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_div_assign);
                        _current++;
                    } else {
                        AddToken(operator_div);
                    }
                    break;
                case punctuation_percent_sign:
                    if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_mod_assign);
                        _current++;
                    } else {
                        AddToken(operator_mod);
                    }
                    break;
                case punctuation_ampersand:
                    if (Peek().TokenType == punctuation_ampersand) {
                        if (Peek2().TokenType == punctuation_equals_sign) {
                            AddToken(operator_booland_assign);
                            _current += 2;
                        } else {
                            AddToken(operator_booland);
                            _current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_bitand_assign);
                        _current++;
                    } else {
                        AddToken(operator_bitand);
                    }
                    break;
                case punctuation_caret:
                    if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_bitxor_assign);
                        _current++;
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
                            _current += 2;
                        } else {
                            AddToken(operator_boolor);
                            _current++;
                        }
                    } else if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_bitor_assign);
                        _current++;
                    } else {
                        AddToken(operator_bitor);
                    }
                    break;
                case punctuation_exclamation_mark:
                    if (Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_neq);
                        _current++;
                    } else {
                        AddToken(operator_boolnot);
                    }
                    break;
                case punctuation_dollar_sign:
                    if(Peek().TokenType == punctuation_equals_sign) {
                        AddToken(operator_concat_assign);
                        _current++;
                    } else {
                        AddToken(operator_concat);
                    }
                    break;
                case keyword_else:
                    if (Peek().TokenType == keyword_if) {
                        AddToken(keyword_else_if);
                        _current++;
                    } else {
                        AddToken(keyword_else);
                    }
                    break;
                default:
                    _tokens.Add(t);
                    break;
            }
        }

        private void AddToken(sws_TokenType tokenType) {
            _tokens.Add(new sws_Token(tokenType, _line, _column));
        }

        private sws_Token Next() {
            return _firstPassTokens[_current++];
        }

        private sws_Token Last() {
            if (_current - 2 < 0) {
                return sws_Token.Error();
            }
            return _firstPassTokens[_current - 2];
        }

        private sws_Token Peek() {
            if (AtEnd()) {
                return sws_Token.Error();
            }
            return _firstPassTokens[_current];
        }

        private sws_Token Peek2() {
            if (_current + 1 >= _firstPassTokens.Count) {
                return sws_Token.Error();
            }
            return _firstPassTokens[_current + 1];
        }

        private bool AtEnd() {
            return _current >= _firstPassTokens.Count;
        }
    }
}
