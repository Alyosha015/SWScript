using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWScript.compiler.sws_Compiler;

namespace SWScript.compiler {
    internal class sws_Error : Exception {
        public static bool Suppress;
        public static int Count;

        public static void Reset() {
            Suppress = false;
            Count = 0;
        }

        public string ErrMessage;
        public string Message2;
        public int Line;
        public int Column;
        public sws_Token Token;

        public sws_Error(string message, int line, int column) {
            ErrMessage = message;
            Line = line;
            Column = column;

            if (Suppress) {
                return;
            }

            Count++;

            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine($"[TOKENIZER ERROR] @ line:{line} {message}");

            Console.ForegroundColor = ConsoleColor.DarkRed;

            Console.WriteLine(SourceLines[line - 1]);
            Console.WriteLine(new string(' ', column - 1) + '^');

            Console.ResetColor();
        }

        public sws_Error(sws_Token token, string message, string message2 = "") : base(message2) {
            Token = token;
            ErrMessage = message;
            Message2 = message2;

            if (Suppress) {
                return;
            }

            Count++;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[PARSER ERROR] @ line:{token.NLine} {message}");

            if (token.NColumn != -1) {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(SourceLines[token.NLine - 1]);
                Console.WriteLine(new string(' ', token.NColumn - 1) + '^');
            }

            Console.ResetColor();
        }

        public sws_Error(string message) {
            new sws_Error(sws_Parser.LastToken, message);
        }

        public sws_Error(sws_Error error) {
            if (error.Token != null) {
                new sws_Error(error.Token, error.ErrMessage, error.Message2);
            } else {
                new sws_Error(error.Message, error.Line, error.Column);
            }
        }
    }
}
