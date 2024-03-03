using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWScript.compiler.sws_TokenType;

namespace SWScript.compiler {
    internal enum sws_TokenType {
        //keywords
        keyword_import,
        keyword_lua,
        keyword_if,
        keyword_else,
        keyword_else_if,
        keyword_for,
        keyword_while,
        keyword_continue,
        keyword_break,
        keyword_func,
        keyword_return,
        keyword_local,
        keyword_true,
        keyword_false,
        keyword_null,
        keyword_print,
        keyword_println,
        //NOTE: _keys() and _type() are reserved functions, however not keywords

        //identifiers
        identifier,

        //literals
        literal_number,
        literal_string,

        //assign operators
        operator_assign,                // =
        operator_add_assign,            // +=
        operator_sub_assign,            // -=
        operator_mul_assign,            // *=
        operator_div_assign,            // /=
        operator_floordiv_assign,       // //=
        operator_pow_assign,            // **=
        operator_mod_assign,            // %=
        operator_booland_assign,        // &&=
        operator_boolor_assign,         // ||=
        operator_bitand_assign,         // &=
        operator_bitxor_assign,         // ^=
        operator_bitor_assign,          // |=
        operator_bitshiftleft_assign,   // <<=
        operator_bitshiftright_assign,  // >>=

        //number, bitwise, logical, and string operators
        operator_add,                   // +
        operator_sub,                   // -
        operator_mul,                   // *
        operator_div,                   // /
        operator_floordiv,              // //
        operator_pow,                   // **
        operator_length,                // #
        operator_mod,                   // %
        operator_booland,               // &&
        operator_boolor,                // ||
        operator_boolnot,               // !
        operator_bitand,                // &
        operator_bitxor,                // ^
        operator_bitor,                 // |
        operator_bitnot,                // ~
        operator_bitshiftleft,          // <<
        operator_bitshiftright,         // >>
        operator_postfixincrement,      // x++
        operator_postfixdecrement,      // x--
        operator_prefixincrement,       // ++x
        operator_prefixdecrement,       // --x
        operator_minus,                 // -
        operator_concat,                // $

        //comparison
        operator_eq,                    // ==
        operator_neq,                   // !=
        operator_gt,                    // >
        operator_lt,                    // <
        operator_gte,                   // >=
        operator_lte,                   // <=

        //punctuation
        punctuation_number_sign,        // #
        punctuation_greater_than,       // >
        punctuation_less_than,          // <
        punctuation_equals_sign,        // =
        punctuation_plus,               // +
        punctuation_minus,              // -
        punctuation_asterisk,           // *
        punctuation_slash,              // /
        punctuation_percent_sign,       // %
        punctuation_ampersand,          // &
        punctuation_caret,              // ^
        punctuation_tilde,              // ~
        punctuation_vertical_bar,       // |
        punctuation_exclamation_mark,   // !
        punctuation_dollar_sign,        // $

        punctuation_period,             // .
        punctuation_comma,              // ,
        punctuation_parenthesis_open,   // (
        punctuation_parenthesis_closed, // )
        punctuation_braces_open,        // {
        punctuation_braces_closed,      // }
        punctuation_brackets_open,      // [
        punctuation_brackets_closed,    // ]

        punctuation_doubleplus,         // ++
        punctuation_doubleminus,        // --

        ERROR,                          //used internally by tokenizer / parser
    }

    internal class sws_Token {
        public static readonly sws_TokenType[] AssignmentOperators = {
            operator_assign,                // =
            operator_add_assign,            // +=
            operator_sub_assign,            // -=
            operator_mul_assign,            // *=
            operator_div_assign,            // /=
            operator_floordiv_assign,       // //=
            operator_pow_assign,            // **=
            operator_mod_assign,            // %=
            operator_booland_assign,        // &&=
            operator_boolor_assign,         // ||=
            operator_bitand_assign,         // &=
            operator_bitxor_assign,         // ^=
            operator_bitor_assign,          // |=
            operator_bitshiftleft_assign,   // <<=
            operator_bitshiftright_assign,  // >>=
        };

        public sws_TokenType TokenType;
        public int NLine;
        public int NColumn;
        public object Literal;

        public sws_Token(sws_TokenType tokenType, int nLine, int nColumn, object literal) {
            TokenType = tokenType;
            NLine = nLine;
            NColumn = nColumn;
            Literal = literal;
        }

        public sws_Token(sws_TokenType tokenType, int nLine, int nColumn) {
            TokenType = tokenType;
            NLine = nLine;
            NColumn = nColumn;
            Literal = string.Empty;
        }

        public static sws_Token Error() {
            return new sws_Token(ERROR, -1, -1);
        }

        public static sws_Token Error(sws_Token last) {
            return new sws_Token(ERROR, last.NLine, last.NColumn);
        }

        public bool IsAssignmentOperator() {
            return Array.IndexOf(AssignmentOperators, TokenType) != -1;
        }

        public override string ToString() {
            return TokenType.ToString() + new string(' ', 31 - TokenType.ToString().Length) + new string(' ', 5 - NLine.ToString().Length) + NLine + " / " + new string(' ', 3 - NColumn.ToString().Length) + NColumn + ": " + Literal;
        }
    }
}
