using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWS.Compiler {
    internal class sws_ParserError : Exception {
        public string ErrMessage;
        public string ErrMessage2;
        public sws_Token Token;

        public sws_ParserError(sws_Token token, string message, string message2 = "") : base(message2) {
            Token = token;
            ErrMessage = message;
            ErrMessage2 = message2;
        }

        public sws_ParserError(string message, string message2 = "") : base(message2) {
            ErrMessage = message;
            ErrMessage2 = message2;
        }

        public sws_ParserError(sws_ParserError error) {
            new sws_ParserError(error.Token, error.ErrMessage, error.ErrMessage2);
        }
    }
}
