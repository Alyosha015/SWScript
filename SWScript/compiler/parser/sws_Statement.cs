using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScript.compiler {
    internal enum sws_StatementType {
        Normal,
        IncDec,
        Call,
    }

    internal class sws_Statement {
        /// <summary>
        /// Statement may not need an assignment operator, such as a function call or increment/decrement operation.
        /// </summary>
        public sws_StatementType Type;
        public List<sws_Expression> Expressions;
        public sws_TokenType AssignmentOperator;
        public List<sws_Expression> Variables;
        public bool Local;

        public sws_Statement(sws_StatementType type, List<sws_Expression> expressions, sws_TokenType assignmentOperator, List<sws_Expression> variables, bool local) {
            Type = type;
            Expressions = expressions;
            AssignmentOperator = assignmentOperator;
            Variables = variables;
            Local = local;
        }
    }
}
