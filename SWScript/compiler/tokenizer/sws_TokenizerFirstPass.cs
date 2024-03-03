using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWScript.compiler.sws_Compiler;
using static SWScript.compiler.sws_TokenType;

namespace SWScript.compiler {
    internal class sws_TokenizerFirstPass {
        public static Dictionary<string, sws_TokenType> Keywords = new Dictionary<string, sws_TokenType>() {
            {"import", keyword_import },
            {"lua", keyword_lua },
            {"if", keyword_if },
            {"else", keyword_else },
            //{"else if", keyword_else_if }, //in practice can't be used by first tokenization pass since it contains whitespace.
            {"for", keyword_for },
            {"while", keyword_while },
            {"continue", keyword_continue },
            {"break", keyword_break },
            {"func", keyword_func },
            {"return", keyword_return },
            {"local", keyword_local },
            {"true", keyword_true },
            {"false", keyword_false },
            {"null", keyword_null },
            {"print", keyword_print },
            {"println", keyword_println },
        };

        private static int Line = 1;
        private static int Column = 0;
        private static int Start = 0;
        private static int Current = 0;

        public static void Tokenize() {
            Line = 1;
            Column = 0;
            Start = 0;
            Current = 0;

            FirstPassTokens = new List<sws_Token>();

            while (!AtEnd()) {
                try {
                    Start = Current;
                    NextToken();
                } catch (sws_Error) {
                    
                }
            }
        }

        private static void NextToken() {
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
                case '\n': Line++; Column = 0; break;
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
                        throw new sws_Error($"Unexpected token {Source.Substring(Start, Current - Start)}", Line, Column);
                    }
                    break;
            }
        }

        /// <summary>
        /// Tokenizes strings. Supported escape sequences: \' \" \\ \0 \a \b \f \n \r \t \v \x.
        /// </summary>
        private static void AddString(char startChar) {
            while (!AtEnd()) {
                if (Peek() == startChar && Last() != '\\') {
                    break;
                }

                if (Peek() == '\n') {
                    Line++;
                }

                Next();
            }

            string str = Source.Substring(Start + 1, Current - Start - 1);

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
                                throw new sws_Error($"Invalid \\x escape sequence '\\x{str.Substring(i)}'. Use format '\\xhh', where 'hh' is a two digit hexadecimal number.", Line, Column);
                            }
                            
                            string hex = str.Substring(i + 1, 2);
                            
                            if (!IsHexDigit(hex[0])) {
                                throw new sws_Error($"Invalid \\x escape sequence '\\x{hex}'. Only use characters 0-9 a-f A-F for hex byte.", Line, Column);
                            }
                            
                            if (!IsHexDigit(hex[1])) {
                                throw new sws_Error($"Invalid \\x escape sequence '\\x{hex}. Only use characters 0-9 a-f A-F for hex byte.", Line, Column);
                            }

                            strOut += (char)Convert.ToSByte(hex, 16);

                            i += 2;
                            break;
                        default:
                            throw new sws_Error($"Unknown escape sequence '\\{c}'.", Line, Column);
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
        private static void AddIdentifier() {
            while (IsAlpha(Peek()) || IsDigit(Peek())) {
                Next();
            }

            string str = Source.Substring(Start, Current - Start);

            if (Keywords.ContainsKey(str)) {
                AddToken(Keywords[str]);
                return;
            }

            AddToken(identifier, str);
        }

        /// <summary>
        /// Used to tokenize numbers, supporting decimal (base10), hex (base16), and binary (base2). A number can have underscores in them as long as it isn't the first character, for example '1_000_000'. To indicate the start of a hexadecimal number use 0x, for binary use 0b.
        /// </summary>
        private static void AddNumber() {
            if (IsDigit(Peek()) || Peek() == '.' || Peek() == '_') { // base 10 number
                while (IsDecimalDigit(Peek())) {
                    Next();
                }
                string numberStr = Source.Substring(Start, Current - Start).Replace("_", string.Empty);
                if (numberStr.Split('.').Length > 2) {
                    throw new sws_Error($"Number '{numberStr}' contains multiple decimal points.", Line, Column);
                }
                AddToken(literal_number, double.Parse(numberStr));
            } else if (Peek() == 'x') { // base 16 number
                Next();
                while (IsHexDigit(Peek())) {
                    Next();
                }
                string numberStr = Source.Substring(Start + 2, Current - Start - 2).Replace("_", string.Empty);
                AddToken(literal_number, (double)Convert.ToInt64(numberStr, 16));
            } else if (Peek() == 'b') { // base 2 number
                Next();
                while (IsBinaryDigit(Peek())) {
                    Next();
                }
                string numberStr = Source.Substring(Start + 2, Current - Start - 2).Replace("_", string.Empty);
                AddToken(literal_number, (double)Convert.ToInt64(numberStr, 2));
            } else {
                AddToken(literal_number, double.Parse(Last().ToString()));
            }
        }

        private static void AddToken(sws_TokenType tokenType) {
            FirstPassTokens.Add(new sws_Token(tokenType, Line, Column));
        }

        private static void AddToken(sws_TokenType tokenType, object literal) {
            FirstPassTokens.Add(new sws_Token(tokenType, Line, Column, literal));
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

        private static bool NextIs(char c) {
            if (Peek() == c) {
                Next();
                return true;
            }
            return false;
        }

        private static char Next() {
            Column++;
            return Source[Current++];
        }

        private static char Last() {
            return Source[Current - 1];
        }

        private static char Peek() {
            if (AtEnd()) {
                return '\0';
            }
            return Source[Current];
        }

        private static bool AtEnd() {
            return Current >= Source.Length;
        }
    }
}
