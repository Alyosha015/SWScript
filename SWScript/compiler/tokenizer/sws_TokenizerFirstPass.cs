using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWS.Compiler.sws_TokenType;

namespace SWS.Compiler {
    internal class sws_TokenizerFirstPass {
        public static readonly Dictionary<string, sws_TokenType> Keywords = new Dictionary<string, sws_TokenType>() {
            {"import", keyword_import },
            {"lua", keyword_lua },
            {"if", keyword_if },
            {"else", keyword_else },
            //{"else if", keyword_else_if }, //in practice can't be used by first tokenization pass since it contains whitespace.
            {"for", keyword_for },
            {"while", keyword_while },
            {"continue", keyword_continue },
            {"switch", keyword_switch },
            {"intswitch", keyword_intswitch },
            {"case", keyword_case },
            {"default", keyword_default },
            {"break", keyword_break },
            {"func", keyword_func },
            {"return", keyword_return },
            {"local", keyword_local },
            {"true", keyword_true },
            {"false", keyword_false },
            {"null", keyword_null },
            {"print", keyword_print },
            {"println", keyword_println },
            {"@property", keyword_property },
        };

        private List<sws_Token> _firstPassTokens;

        private string _source;
        
        private int _line = 1;
        private int _column = 0;
        private int _start = 0;
        private int _current = 0;

        public List<sws_Token> Tokenize(SWScript sws, string source, string[] sourceLines) {
            _source = source;

            _firstPassTokens = new List<sws_Token>();

            _line = 1;
            _column = 0;
            _start = 0;
            _current = 0;

            while (!AtEnd()) {
                try {
                    _start = _current;
                    NextToken();
                } catch (sws_TokenizerError e) {
                    sws.MessageAction(MsgType.ErrTokenize, e.Line, e.Column, sourceLines[e.Line - 1], e.ErrMessage);
                }
            }

            _firstPassTokens.Add(new sws_Token(EOF, -1, -1));

            return _firstPassTokens;
        }

        private void NextToken() {
            char c = Next();

            switch (c) {
                case '#': AddToken(punctuation_number_sign); break;
                case '>': AddToken(punctuation_greater_than); break;
                case '<': AddToken(punctuation_less_than); break;
                case '=': AddToken(punctuation_equals_sign); break;
                case '+': AddToken(punctuation_plus); break;
                case '-': AddToken(punctuation_minus); break;
                case '*': AddToken(punctuation_asterisk); break;
                case '/': AddToken(punctuation_slash); break;
                case '%': AddToken(punctuation_percent_sign); break;
                case '&': AddToken(punctuation_ampersand); break;
                case '^': AddToken(punctuation_caret); break;
                case '~': AddToken(punctuation_tilde); break;
                case '|': AddToken(punctuation_vertical_bar); break;
                case '!': AddToken(punctuation_exclamation_mark); break;
                case '$': AddToken(punctuation_dollar_sign); break;
                case ':': AddToken(punctuation_colon); break;
                case '.': AddToken(punctuation_period); break;
                case ',': AddToken(punctuation_comma); break;
                case '(': AddToken(punctuation_parenthesis_open); break;
                case ')': AddToken(punctuation_parenthesis_closed); break;
                case '{': AddToken(punctuation_braces_open); break;
                case '}': AddToken(punctuation_braces_closed); break;
                case '[': AddToken(punctuation_brackets_open); break;
                case ']': AddToken(punctuation_brackets_closed); break;

                case ';': //comment
                    while (!AtEnd() && Peek() != '\n') {
                        Next();
                    }
                    break;

                case ' ': break; //whitespace
                case '\n': _line++; _column = 0; break;
                case '\r': break;
                case '\t': break;

                case '\'': AddString('\''); break;
                case '"': AddString('"'); break;

                default:
                    if (IsAlpha(c)) {
                        AddIdentifier();
                    } else if (IsDigit(c)) {
                        AddNumber();
                    } else {
                        throw new sws_TokenizerError($"Unexpected string {_source.Substring(_start, _current - _start)}", _line, _column);
                    }
                    break;
            }
        }

        /// <summary>
        /// Tokenizes strings. Supported escape sequences: \' \" \\ \0 \a \b \f \n \r \t \v \x.
        /// </summary>
        private void AddString(char startChar) {
            while (!AtEnd()) {
                if (Peek() == startChar && ((Last() == '\\' && LastLast() == '\\') || Last() != '\\')) {
                    break;
                }

                if (Peek() == '\n') {
                    _line++;
                }

                Next();
            }

            string str = _source.Substring(_start + 1, _current - _start - 1);

            if (!AtEnd()) {
                Next();
            }

            //handle escape sequences
            string strOut = string.Empty;

            for (int i = 0; i < str.Length; i++) {
                char c = str[i];
                if (c == '\\') {
                    i++;
                    c = str[i];
                    switch (c) {
                        case '\'':
                            strOut += '\'';
                            break;
                        case '"':
                            strOut += '"';
                            break;
                        case '\\':
                            strOut += '\\';
                            break;
                        case '0':
                            strOut += '\0';
                            break;
                        case 'a':
                            strOut += '\a';
                            break;
                        case 'b':
                            strOut += '\b';
                            break;
                        case 'f':
                            strOut += '\f';
                            break;
                        case 'n':
                            strOut += '\n';
                            break;
                        case 'r':
                            strOut += '\r';
                            break;
                        case 't':
                            strOut += '\t';
                            break;
                        case 'v':
                            strOut += '\v';
                            break;
                        case 'x':
                            if (i + 2 >= str.Length) {
                                throw new sws_TokenizerError($"Invalid \\x escape sequence '\\x{str.Substring(i)}'. Use format '\\xhh', where 'hh' is a two digit hexadecimal number.", _line, _column);
                            }
                            
                            string hex = str.Substring(i + 1, 2);
                            
                            if (!IsHexDigit(hex[0])) {
                                throw new sws_TokenizerError($"Invalid \\x escape sequence '\\x{hex}'. Only use characters 0-9 a-f A-F for hex byte.", _line, _column);
                            }
                            
                            if (!IsHexDigit(hex[1])) {
                                throw new sws_TokenizerError($"Invalid \\x escape sequence '\\x{hex}. Only use characters 0-9 a-f A-F for hex byte.", _line, _column);
                            }

                            strOut += (char)Convert.ToSByte(hex, 16);

                            i += 2;
                            break;
                        default:
                            throw new sws_TokenizerError($"Unknown escape sequence '\\{c}'.", _line, _column);
                    }
                } else {
                    strOut += c;
                }
            }

            AddToken(literal_string, strOut);
        }

        /// <summary>
        /// Used to tokenize identifiers and keywords. An identifier includes variable names/function names, generally any text which isn't a keyword. An identifier has to start with either a letter or underscore, and can also have numbers in the name afterwards.
        /// </summary>
        private void AddIdentifier() {
            while (IsAlpha(Peek()) || IsDigit(Peek())) {
                Next();
            }

            string str = _source.Substring(_start, _current - _start);

            if (Keywords.ContainsKey(str)) {
                AddToken(Keywords[str]);
                return;
            }

            AddToken(identifier, str);
        }

        /// <summary>
        /// Used to tokenize numbers, supporting decimal (base10), hex (base16), and binary (base2). A number can have underscores in them as long as it isn't the first character, for example '1_000_000'. To indicate the start of a hexadecimal number use 0x, for binary use 0b.
        /// </summary>
        private void AddNumber() {
            if (IsDigit(Peek()) || Peek() == '.' || Peek() == '_') { // base 10 number
                while (IsDecimalDigit(Peek())) {
                    Next();
                }
                string numberStr = _source.Substring(_start, _current - _start).Replace("_", string.Empty);
                if (numberStr.Split('.').Length > 2) {
                    throw new sws_TokenizerError($"Number '{numberStr}' contains multiple decimal points.", _line, _column);
                }
                AddToken(literal_number, double.Parse(numberStr));
            } else if (Peek() == 'x') { // base 16 number
                Next();
                while (IsHexDigit(Peek())) {
                    Next();
                }
                string numberStr = _source.Substring(_start + 2, _current - _start - 2).Replace("_", string.Empty);
                AddToken(literal_number, (double)Convert.ToInt64(numberStr, 16));
            } else if (Peek() == 'b') { // base 2 number
                Next();
                while (IsBinaryDigit(Peek())) {
                    Next();
                }
                string numberStr = _source.Substring(_start + 2, _current - _start - 2).Replace("_", string.Empty);
                AddToken(literal_number, (double)Convert.ToInt64(numberStr, 2));
            } else {
                AddToken(literal_number, double.Parse(Last().ToString()));
            }
        }

        private void AddToken(sws_TokenType tokenType) {
            _firstPassTokens.Add(new sws_Token(tokenType, _line, _column));
        }

        private void AddToken(sws_TokenType tokenType, object literal) {
            _firstPassTokens.Add(new sws_Token(tokenType, _line, _column, literal));
        }

        private static bool IsAlpha(char c) {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        }

        private static bool IsDigit(char c) {
            return c >= '0' && c <= '9';
        }

        private static bool IsDecimalDigit(char c) {
            return IsDigit(c) || c == '_' || c == '.';
        }

        private static bool IsHexDigit(char c) {
            return IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || c == '_';
        }

        private static bool IsBinaryDigit(char c) {
            return c == '0' || c == '1' || c == '_';
        }

        private char Next() {
            _column++;
            return _source[_current++];
        }

        private char Last() {
            if (_current - 1 < 0) {
                return '\0';
            }
            return _source[_current - 1];
        }

        private char LastLast() {
            if(_current - 2 < 0) {
                return '\0';
            }
            return _source[_current - 2];
        }

        private char Peek() {
            if (AtEnd()) {
                return '\0';
            }
            return _source[_current];
        }

        private bool AtEnd() {
            return _current >= _source.Length;
        }
    }
}
