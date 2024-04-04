using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWScript.compiler.sws_Compiler;
using static SWScript.compiler.sws_TokenType;
using static SWScript.compiler.sws_Opcode;

namespace SWScript.compiler {
    internal class sws_Parser {
        /// <summary>
        /// Used as part of error handling, stores last token access with Next() / Peek() / etc.
        /// </summary>
        public static sws_Token LastToken;

        /// <summary>
        /// "Depth" of scope parser is currently in.
        /// </summary>
        public static int Depth;

        /// <summary>
        /// Index of current token being read by parser.
        /// </summary>
        private static int current;

        /// <summary>
        /// Used to keep track of index of break and continue statement jump instructions at each depth.
        /// </summary>
        private static Stack<List<int>> continueBreakJumpLocations;

        private static Stack<List<int>> ifEndJumpLocations;

        public static void Parse() {
            current = 0;
            Depth = 0;

            continueBreakJumpLocations = new Stack<List<int>>();
            ifEndJumpLocations = new Stack<List<int>>();

            Prg = new sws_Prg(SourceLines);

            Prg.PushFrame(string.Empty);

            while (!AtEnd()) {
                try {
                    ParserMain();
                } catch (sws_Error) {
                    Synchronize();
                }
            }

            Prg.Frames[string.Empty].
                Program.Add(new sws_Op(op_halt, int.MinValue));

            Prg.SetLocalScopeEnd();

            Prg.Export();
            
            Prg.Compiled = true;
        }

        //parser functions start here

        private static void ParserMain() {
            if (Match(keyword_func)) {
                ParseFunction();
            } else if (Match(keyword_if)) {
                IfStatement();
            } else if (Match(keyword_for)) {
                Depth++;
                continueBreakJumpLocations.Push(new List<int>());
                ForStatement();
                continueBreakJumpLocations.Pop();
                Depth--;
            } else if (Match(keyword_while)) {
                Depth++;
                continueBreakJumpLocations.Push(new List<int>());
                WhileStatement();
                continueBreakJumpLocations.Pop();
                Depth--;
            } else if (Match(keyword_continue)) {
                Prg.AddInstruction(op_jmp, JMP_CONTINUE);
                continueBreakJumpLocations.Peek().Add(Prg.GetPC());
            } else if (Match(keyword_break)) {
                Prg.AddInstruction(op_jmp, JMP_BREAK);
                continueBreakJumpLocations.Peek().Add(Prg.GetPC());
            } else if (Match(keyword_return)) {
                ReturnStatement();
            } else if (Peek().TokenType == keyword_print || Peek().TokenType == keyword_println) {
                bool println = Peek().TokenType == keyword_println;
                Next();
                PrintStatement(println);
            } else if ((Peek().TokenType == identifier && Peek2().TokenType != punctuation_parenthesis_open) || Peek().TokenType == keyword_local) {
                sws_Statement statement = ExpressionStatement(true);
                if (statement != null) {
                    StatementToBytecode(statement);
                }
            } else if (Peek().TokenType == identifier || Peek().TokenType == keyword_lua) { //most function calls generated here
                ExprToBytecode(Expression(), true, false);
            } else {
                throw new sws_Error(Peek(), $"Unexpected token '{Peek().TokenType}'");
            }
        }

        private static void ParseFunction(bool expectNoName = false) {
            if (expectNoName && Peek().TokenType != punctuation_parenthesis_open) {
                if(Peek().TokenType == identifier) {
                    throw new sws_Error(Peek(), "A function in an expression can't have a name. Expected 'func()'.");
                } else {
                    throw new sws_Error(Peek(), "Expected '('.");
                }
            }

            //parse function name and push new frame for function
            if (Peek().TokenType != punctuation_parenthesis_open) {
                Consume(identifier, "Expected function name.");
                Prg.PushFrame(Last().Literal.ToString());
            } else {
                Prg.PushNoName();
            }

            Depth++;

            //parse function arguments.
            Consume(punctuation_parenthesis_open, "Expected '('.");

            List<string> parameters = new List<string>();
            while(Peek().TokenType != punctuation_parenthesis_closed) {
                parameters.Add(Consume(identifier, "Expected variable in function parameters").Literal.ToString());

                if(Peek().TokenType != punctuation_parenthesis_closed) {
                    Consume(punctuation_comma, "Expected ',' between function parameters.");

                    if (Peek().TokenType == punctuation_parenthesis_closed) {
                        throw new sws_Error(Peek(), "Expected function parameter after ','.");
                    }
                }
            }

            Consume(punctuation_parenthesis_closed, "Expected ')'.");

            for (int i = 0; i < parameters.Count; i++) {
                Prg.AddLocal(parameters[i]);
            }

            //parse function body
            Consume(punctuation_braces_open, "Expected '{' to begin function body.");
            Prg.SetLocalScopeStart();

            while (!AtEnd() && (Peek().TokenType != punctuation_braces_closed)) {
                ParserMain();
            }

            Prg.SetLocalScopeEnd();
            Consume(punctuation_braces_closed, "Expected '}' to end function body.");

            //adds return statement if there's none
            if (Prg.Frame.Program.Count == 0 || Prg.Frame.Program.Last().Opcode != op_return) {
                Prg.AddInstruction(op_return, 0);
            }

            Prg.PopFrame();

            Depth--;
        }

        private static void IfStatement() {
            int pc;
            ifEndJumpLocations.Push(new List<int>());

            sws_Expression condition = Expression();
            List<int> jumpsToResolve = ExprToBytecode(condition, true, true, true);
            Depth++;

            Prg.AddJumpFInstruction(JMP_IFEND);
            pc = Prg.GetPC();

            Consume(punctuation_braces_open, "Expected '{'.");

            Body();
            Prg.SetLocalScopeEnd();

            //jumps to end of if-else if-else chain, only add if it's not the last
            if (Peek().TokenType == keyword_else_if || Peek().TokenType == keyword_else) {
                Prg.AddInstruction(op_jmp, JMP_IFEND);
                ifEndJumpLocations.Peek().Add(Prg.GetPC());
            }

            Prg.ResolveJumpPC(pc);
            Prg.ResolveJumpPCs(jumpsToResolve);

            Depth--;

            while(Match(keyword_else_if)) {
                sws_Expression ifElseCondition = Expression();
                jumpsToResolve = ExprToBytecode(ifElseCondition, true, true, true);

                Depth++;

                Prg.AddJumpFInstruction(JMP_IFEND);
                pc = Prg.GetPC();

                Consume(punctuation_braces_open, "Expected '{'.");

                Body();
                Prg.SetLocalScopeEnd();

                //jumps to end of if-else if-else chain, only add if it's not the last
                if(Peek().TokenType == keyword_else_if || Peek().TokenType == keyword_else) {
                    Prg.AddInstruction(op_jmp, JMP_IFEND);
                    ifEndJumpLocations.Peek().Add(Prg.GetPC());
                }

                Prg.ResolveJumpPC(pc);
                Prg.ResolveJumpPCs(jumpsToResolve);

                Depth--;
            }

            if (Match(keyword_else)) {
                Consume(punctuation_braces_open, "Expected '{'.");

                Depth++;

                Body();
                Prg.SetLocalScopeEnd();

                Depth--;
            }

            Prg.ResolveIfEnd(ifEndJumpLocations.Peek(), Prg.GetPC());

            ifEndJumpLocations.Pop();
        }

        /// <summary>
        /// Parses for statement. A for can be formated two ways: 'for a, b {}' and 'for a, b, c {}'. a and b are identical for both, with a creating the iterator variable (<name>=<expression>), and b being the condition expression for the loop. In the second type, however, c is an expression which executes at the end of each loop. If c is not present the variable in a is declared instead.
        /// </summary>
        private static void ForStatement() {
            //a
            Consume(identifier, "Expected identifer for iterator variable's name.");

            string iterator = Last().Literal.ToString();

            Consume(operator_assign, "Expected '='.");
            
            sws_Expression iteratorInitializer = Expression();

            Prg.AddVariable(iterator, true);
            ExprToBytecode(iteratorInitializer);
            Prg.SetLocalScopeStart();
            Prg.AddVarSetInstruction(iterator);

            //b

            Consume(punctuation_comma, "Expected ','.");

            sws_Expression condition = Expression();

            //c
            //uses parser for statement
            bool hasCTerm = false;
            sws_Statement statement = null;
            
            if (Peek().TokenType != punctuation_braces_open) {
                hasCTerm = true;

                Consume(punctuation_comma, "Expected ','.");

                //don't generate bytecode yet, it will be done after the body is parsed.
                statement = ExpressionStatement(false);
            }

            Consume(punctuation_braces_open, "Expected '{'.");

            //condition. Runs before loop body.
            int conditionStartPc = Prg.GetPC();
            List<int> jumpsToResolve = ExprToBytecode(condition, true, true, true);
            Prg.AddJumpFInstruction(JMP_CONDITION);
            int jfpc = Prg.GetPC();

            Body();

            //logic for incrementing and looping back to checking condition
            int incrementStartPC = Prg.GetPC();

            if (!hasCTerm) {
                Prg.AddVarGetInstruction(iterator);
                Prg.AddInstruction(op_addimm, 1);
                Prg.AddVarSetInstruction(iterator);
            } else {
                StatementToBytecode(statement);
            }

            Prg.AddInstruction(op_jmp, conditionStartPc - Prg.GetPC() - 1);

            //

            Prg.ResolveJumpPC(jfpc);
            Prg.ResolveJumpPCs(jumpsToResolve);

            Prg.ResolveContinueBreakLoop(continueBreakJumpLocations.Peek(), incrementStartPC, Prg.GetPC());

            Prg.SetLocalScopeEnd();
        }

        private static void WhileStatement() {
            sws_Expression condition = Expression();

            Consume(punctuation_braces_open, "Expected '{'.");

            int conditionStartPc = Prg.GetPC();
            List<int> jumpsToResolve = ExprToBytecode(condition, true, true, true);
            Prg.AddJumpFInstruction(JMP_CONDITION);
            int jfpc = Prg.GetPC();

            Body();

            Prg.AddInstruction(op_jmp, conditionStartPc - Prg.GetPC() - 1);

            //

            Prg.ResolveJumpPC(jfpc);
            Prg.ResolveJumpPCs(jumpsToResolve);

            Prg.ResolveContinueBreakLoop(continueBreakJumpLocations.Peek(), conditionStartPc, Prg.GetPC());

            Prg.SetLocalScopeEnd();
        }

        private static void ReturnStatement() {
            int returnValuesCount = 0;

            while (true) {
                sws_Expression expr = TryGetExpression();

                if (expr == null) {
                    break;
                }

                returnValuesCount++;

                ExprToBytecode(expr);

                //consume commas, and break from loop if there is no comma following the expression, which is assumed to be the end of the return arguments.
                if (!Match(punctuation_comma)) {
                    break;
                }
            }

            Prg.AddInstruction(op_return, returnValuesCount);
        }

        private static void PrintStatement(bool println) {
            Consume(punctuation_parenthesis_open, "Expected '(' after print.");

            //print statement with no arguments.
            if (Match(punctuation_parenthesis_closed)) {
                Prg.AddInstruction(op_getconst, Prg.AddConst(string.Empty, sws_DataType.String));
                Prg.AddInstruction(op_print, println ? 0 : 1);
                return;
            }

            sws_Expression expr = Expression();
            ExprToBytecode(expr);

            Prg.AddInstruction(op_print, println ? 0 : 1);

            Consume(punctuation_parenthesis_closed, "Expected ')' after print function argument.");
        }

        private static void Body() {
            while (!Match(punctuation_braces_closed)) {
                ParserMain();
            }
        }

        /// <summary>
        /// Variable assignment and table set statements. This complicated by the fact that there can be multiple assignments in a single statement, for example 'a,b=b,a' to swap variables. Additionally, operators such as += can also be used, even with multiple variables. When there are multiple variables used the same amount is expected on the other side, with the exception of function calls which can return multiple variables, but in that case only one function call and no other expressions are allowed because I don't want to deal with it. Additionally, this code handles increment/decrement statements. I would also like to apologize in advance for the spaghetti code, there are way too many combinations of these conditions. (This code is in the function StatementToBytecode()).
        /// 
        /// This function was split up from StatementToBytecode so that bytecode could be generated after a statement was parsed, which is used when parsing for loops for the third part of the statement, which needs to be parsed before the body and generated afterward.
        /// </summary>
        private static sws_Statement ExpressionStatement(bool generateBytecode) {
            bool local = Match(keyword_local);

            List<sws_Expression> variables = new List<sws_Expression>(); //left side of assignment
            List<sws_Expression> expressions = new List<sws_Expression>(); //right side of assignment

            sws_TokenType assignmentOperator;

            while (!Peek().IsAssignmentOperator()) {
                variables.Add(Expression());

                //check for x++ type statements and function calls
                if (variables.Count == 1) {
                    sws_TokenType op = variables[0].Op;

                    if (op == operator_prefixincrement || op == operator_prefixdecrement || op == operator_postfixincrement || op == operator_postfixdecrement) {
                        if (generateBytecode) {
                            ExprToBytecode(variables[0], false);
                            return null;
                        } else {
                            return new sws_Statement(sws_StatementType.IncDec, null, ERROR, variables, local);
                        }
                    }

                    //function calls
                    if (variables[0].Type == sws_ExpressionType.ClosureCall) {
                        if (generateBytecode) {
                            ExprToBytecode(variables[0], true, false);
                            return null;
                        } else {
                            return new sws_Statement(sws_StatementType.Call, null, ERROR, variables, local);
                        }
                    }

                    //self function calls

                    if (variables[0].Type == sws_ExpressionType.SelfCall) {
                        if (generateBytecode) {
                            ExprToBytecode(variables[0]);
                            return null;
                        } else {
                            return null;
                        }
                    }
                }

                if (!Peek().IsAssignmentOperator()) {
                    Consume(punctuation_comma, "Expected ',' between variables.");
                }
            }

            assignmentOperator = Next().TokenType;

            //prevent code like 'local a += 10', which isn't possible
            if (local && assignmentOperator != operator_assign) {
                throw new sws_Error(Peek(), $"Can't use '{assignmentOperator}' when declaring local variable. Use '=' instead.");
            }

            while (true) {
                expressions.Add(Expression());
                if (!Match(punctuation_comma)) {
                    break;
                }
            }

            return new sws_Statement(sws_StatementType.Normal, expressions, assignmentOperator, variables, local);
        }

        private static void StatementToBytecode(sws_Statement statement) {

            sws_StatementType type = statement.Type;
            List<sws_Expression> expressions = statement.Expressions;
            sws_TokenType assignmentOperator = statement.AssignmentOperator;
            List<sws_Expression> variables = statement.Variables;
            bool local = statement.Local;

            if (type == sws_StatementType.IncDec) {
                ExprToBytecode(variables[0], false);
                return;
            } else if (type == sws_StatementType.Call) {
                ExprToBytecode(variables[0], true, false);
                return;
            }

            bool functionCallSpecialCase = expressions.Count == 1 && (expressions[0].Type == sws_ExpressionType.LuaCall || expressions[0].Type == sws_ExpressionType.ClosureCall);

            if (variables.Count != expressions.Count && !functionCallSpecialCase) {
                throw new sws_Error(Last(), $"Number of variables on left ({variables.Count}) does not match number of expressions on right ({expressions.Count}). This is only valid when the right side is a single function call.");
            }

            if (!functionCallSpecialCase) {
                for (int i = 0; i < expressions.Count; i++) {
                    sws_Expression variable = variables[i];
                    sws_Expression expr = expressions[i];

                    bool table = variable.Type == sws_ExpressionType.TableGet;

                    //left side, ensure variables exist
                    Prg.AddVariable(variable.Name, local);

                    //right side of statement (don't do this for && and ||)
                    if (assignmentOperator != operator_booland_assign && assignmentOperator != operator_boolor_assign) {
                        ExprToBytecode(expr);
                    }

                    //'a += 10' is interpreted as 'a = 10 + a', this generates the bytecode for the '+ a' part.
                    if (assignmentOperator != operator_assign) {
                        AssignOperatorCase(variable, expr);
                    }

                    if (table) {
                        ExprToBytecode(variable.Indices.Last());
                        TableGetNoLastIndex(variable);
                        Prg.AddInstruction(op_tableset, 1);
                    } else {
                        Prg.AddVarSetInstruction(variable.Name);
                    }
                }
            } else {
                //left side, ensure variables exist
                for (int i = 0; i < variables.Count; i++) {
                    Prg.AddVariable(variables[i].Name, local);
                }

                //right side of statement (don't do this for boolean and / or)
                if (assignmentOperator != operator_booland_assign && assignmentOperator != operator_boolor_assign) {
                    ExprToBytecode(expressions[0]);
                }

                for (int i = variables.Count - 1; i >= 0; i--) {
                    sws_Expression variable = variables[i];

                    if (assignmentOperator != operator_assign) {
                        AssignOperatorCase(variable, expressions[0]);
                    }

                    bool table = variable.Type == sws_ExpressionType.TableGet;

                    if (table) {
                        ExprToBytecode(variable.Indices.Last());
                        TableGetNoLastIndex(variable);
                        Prg.AddInstruction(op_tableset, 1);
                    } else {
                        Prg.AddVarSetInstruction(variable.Name);
                    }
                }
            }

            Prg.SetLocalScopeStart();

            void AssignOperatorCase(sws_Expression variable, sws_Expression expression) {
                ExprToBytecode(variable);

                switch (assignmentOperator) {
                    case operator_add_assign:
                        Prg.AddInstruction(op_add);
                        break;
                    case operator_sub_assign:
                        Prg.AddInstruction(op_sub);
                        break;
                    case operator_mul_assign:
                        Prg.AddInstruction(op_mul);
                        break;
                    case operator_div_assign:
                        Prg.AddInstruction(op_div);
                        break;
                    case operator_floordiv_assign:
                        Prg.AddInstruction(op_floordiv);
                        break;
                    case operator_pow_assign:
                        Prg.AddInstruction(op_pow);
                        break;
                    case operator_mod_assign:
                        Prg.AddInstruction(op_mod);
                        break;
                    case operator_booland_assign: {
                            Prg.AddInstruction(op_jfnp);
                            int pc = Prg.GetPC();
                            
                            ExprToBytecode(expression);

                            Prg.ResolveJumpPC(pc);
                            break;
                        }
                    case operator_boolor_assign: {
                            Prg.AddInstruction(op_jtnp);
                            int pc = Prg.GetPC();

                            ExprToBytecode(expression);
                            
                            Prg.ResolveJumpPC(pc);
                            break;
                        }
                    case operator_bitand_assign:
                        Prg.AddInstruction(op_bitand);
                        break;
                    case operator_bitxor_assign:
                        Prg.AddInstruction(op_bitxor);
                        break;
                    case operator_bitor_assign:
                        Prg.AddInstruction(op_bitor);
                        break;
                    case operator_bitshiftleft_assign:
                        Prg.AddInstruction(op_bitshiftleft);
                        break;
                    case operator_bitshiftright_assign:
                        Prg.AddInstruction(op_bitshiftright);
                        break;
                }
            }
        }

        //Similar to regular table get, but doesn't get the last index. This is primarly used to set tables in expression statements.
        private static void TableGetNoLastIndex(sws_Expression variable) {
            Prg.AddVarGetInstruction(variable.Name);

            for (int i = 0; i < variable.Indices.Count - 1; i++) {
                ExprToBytecode(variable.Indices[i]);
                Prg.AddInstruction(op_tableget);
            }
        }

        //functions for parsing expressions. Only ExprToBytecode, TryGetExpression, and Expression are called by other parts of the parser. The remaining functions are a part of a recursive descent parser for expressions.

        private static List<int> ExprToBytecode(sws_Expression expression, bool incDecReturnValues = true, bool callReturnValues = true, bool conditionalExpr = false) {
            int flagModCounter = 0;

            int pcStart = Prg.GetPC();
            pcStart = Math.Max(0, pcStart);

            List<int> conditionalSkipJmpsPCs = new List<int>();
            List<int> conditionalSkipJmpsExceptions = new List<int>();

            expression = expression.ConstantFold();

            ExprToBytecodeRecursive(expression);

            if (conditionalExpr) {
                for (int i = pcStart; i < Prg.Frame.Program.Count; i++) {                    
                    sws_Op opcode = Prg.Frame.Program[i];

                    if (opcode.IsJumpOp() && (i + opcode.Data - Prg.Frame.Program.Count == -1) && !conditionalSkipJmpsExceptions.Contains(i)) {
                        conditionalSkipJmpsPCs.Add(i);
                    }
                }
            }

            bool jfalseCanSimplify = Prg.JumpFCanSimplify();

            if (jfalseCanSimplify) {
                for (int i = pcStart; i < Prg.Frame.Program.Count; i++) {
                    sws_Op opcode = Prg.Frame.Program[i];

                    if (opcode.IsJumpOp() && (i + opcode.Data - Prg.Frame.Program.Count == 0)) {
                        opcode.Data--;
                    }
                }
            }

            return conditionalSkipJmpsPCs;

            void ExprToBytecodeRecursive(sws_Expression expr) {
                switch (expr.Type) {
                    case sws_ExpressionType.Unary: {
                            if (expr.Op != operator_prefixdecrement && expr.Op != operator_prefixincrement && expr.Op != operator_postfixdecrement && expr.Op != operator_postfixincrement) {
                                ExprToBytecodeRecursive(expr.Right);
                            }

                            switch (expr.Op) {
                                case operator_boolnot: {
                                        Prg.AddInstruction(op_boolnot);
                                        break;
                                    }
                                case operator_bitnot: {
                                        Prg.AddInstruction(op_bitnot);
                                        break;
                                    }
                                case operator_minus: {
                                        Prg.AddInstruction(op_minus);
                                        break;
                                    }
                                case operator_length: {
                                        Prg.AddInstruction(op_len);
                                        break;
                                    }
                                case operator_prefixincrement: {
                                        bool table = expr.Right.Type == sws_ExpressionType.TableGet;

                                        ExprToBytecodeRecursive(expr.Right);

                                        Prg.AddInstruction(op_addimm, 1);

                                        if (incDecReturnValues) {
                                            Prg.AddInstruction(op_dup);
                                        }

                                        if (table) {
                                            ExprToBytecodeRecursive(expr.Right.Indices.Last());
                                            TableGetNoLastIndex(expr.Right);
                                            Prg.AddInstruction(op_tableset, 1);
                                        } else {
                                            Prg.AddVarSetInstruction(expr.Right.Name);
                                        }

                                        break;
                                    }
                                case operator_prefixdecrement: {
                                        bool table = expr.Right.Type == sws_ExpressionType.TableGet;

                                        ExprToBytecodeRecursive(expr.Right);

                                        Prg.AddInstruction(op_addimm, -1);

                                        if (incDecReturnValues) {
                                            Prg.AddInstruction(op_dup);
                                        }

                                        if (table) {
                                            ExprToBytecodeRecursive(expr.Right.Indices.Last());
                                            TableGetNoLastIndex(expr.Right);
                                            Prg.AddInstruction(op_tableset, 1);
                                        } else {
                                            Prg.AddVarSetInstruction(expr.Right.Name);
                                        }

                                        break;
                                    }
                                case operator_postfixincrement: {
                                        bool table = expr.Right.Type == sws_ExpressionType.TableGet;

                                        ExprToBytecodeRecursive(expr.Right);

                                        if(incDecReturnValues) {
                                            Prg.AddInstruction(op_dup);
                                        }

                                        Prg.AddInstruction(op_addimm, 1);

                                        if (table) {
                                            ExprToBytecodeRecursive(expr.Right.Indices.Last());
                                            TableGetNoLastIndex(expr.Right);
                                            Prg.AddInstruction(op_tableset, 1);
                                        } else {
                                            Prg.AddVarSetInstruction(expr.Right.Name);
                                        }

                                        break;
                                    }
                                case operator_postfixdecrement: {
                                        bool table = expr.Right.Type == sws_ExpressionType.TableGet;

                                        ExprToBytecodeRecursive(expr.Right);

                                        if (incDecReturnValues) {
                                            Prg.AddInstruction(op_dup);
                                        }

                                        Prg.AddInstruction(op_addimm, -1);

                                        if (table) {
                                            ExprToBytecodeRecursive(expr.Right.Indices.Last());
                                            TableGetNoLastIndex(expr.Right);
                                            Prg.AddInstruction(op_tableset, 1);
                                        } else {
                                            Prg.AddVarSetInstruction(expr.Right.Name);
                                        }

                                        break;
                                    }
                            }
                            break;
                        }
                    case sws_ExpressionType.Binary: {
                            if (expr.Op != operator_booland && expr.Op != operator_boolor) {
                                if (expr.Op == operator_gt || expr.Op == operator_gte) {
                                    ExprToBytecodeRecursive(expr.Left);
                                    ExprToBytecodeRecursive(expr.Right);
                                } else {
                                    ExprToBytecodeRecursive(expr.Right);
                                    ExprToBytecodeRecursive(expr.Left);
                                }
                            }

                            switch (expr.Op) {
                                case operator_boolor: {
                                        ExprToBytecodeOr(expr);
                                        break;
                                    }
                                case operator_booland: {
                                        ExprToBytecodeAnd(expr, (expr.Parent != null && expr.Parent.Op == operator_booland && expr.Parent.Left == expr) || (expr.Parent != null && expr.Parent.Parent != null && expr.Parent.Parent.Op == operator_booland && expr.Parent.Parent.Right.Left == expr));
                                        break;
                                    }
                                case operator_bitor:
                                    Prg.AddInstruction(op_bitand);
                                    break;
                                case operator_bitxor:
                                    Prg.AddInstruction(op_bitxor);
                                    break;
                                case operator_bitand:
                                    Prg.AddInstruction(op_bitand);
                                    break;
                                case operator_eq:
                                    Prg.AddInstruction(op_eq);
                                    break;
                                case operator_neq:
                                    Prg.AddInstruction(op_neq);
                                    break;
                                case operator_gt:
                                    Prg.AddInstruction(op_lt);
                                    break;
                                case operator_gte:
                                    Prg.AddInstruction(op_lte);
                                    break;
                                case operator_lt:
                                    Prg.AddInstruction(op_lt);
                                    break;
                                case operator_lte:
                                    Prg.AddInstruction(op_lte);
                                    break;
                                case operator_bitshiftleft:
                                    Prg.AddInstruction(op_bitshiftleft);
                                    break;
                                case operator_bitshiftright:
                                    Prg.AddInstruction(op_bitshiftright);
                                    break;
                                case operator_add:
                                    Prg.AddInstruction(op_add);
                                    break;
                                case operator_sub:
                                    Prg.AddInstruction(op_sub);
                                    break;
                                case operator_mul:
                                    Prg.AddInstruction(op_mul);
                                    break;
                                case operator_div:
                                    Prg.AddInstruction(op_div);
                                    break;
                                case operator_floordiv:
                                    Prg.AddInstruction(op_floordiv);
                                    break;
                                case operator_mod:
                                    Prg.AddInstruction(op_mod);
                                    break;
                                case operator_pow:
                                    Prg.AddInstruction(op_pow);
                                    break;
                                case operator_concat:
                                    Prg.AddInstruction(op_concat, 0);
                                    break;
                            }
                            break;
                        }
                    case sws_ExpressionType.Literal: {
                            if (ValidImmediateValue(expr)) {
                                Prg.AddInstruction(op_loadimm, (int)(double)expr.Value);
                                break;
                            }

                            if (expr.ValueType == sws_DataType.Bool) {
                                Prg.AddInstruction(op_loadbool, (bool)expr.Value ? 1 : 0);
                                break;
                            }

                            if (expr.ValueType == sws_DataType.Null) {
                                Prg.AddInstruction(op_loadnull);
                                break;
                            }

                            Prg.AddInstruction(op_getconst, Prg.AddConst(expr.Value, expr.ValueType));

                            break;
                        }
                    case sws_ExpressionType.Variable: {
                            Prg.AddVarGetInstruction(expr.Name);
                            break;
                        }
                    case sws_ExpressionType.Table: {
                            for (int i = expr.Elements.Count - 1; i >= 0; i--) {
                                ExprToBytecodeRecursive(expr.Elements[i]);
                                ExprToBytecodeRecursive(expr.ElementIndices[i]);
                            }

                            Prg.AddInstruction(op_tablenew);

                            if (expr.Elements.Count > 0) {
                                Prg.AddInstruction(op_tableset, -expr.Elements.Count);
                            }
                            break;
                        }
                    case sws_ExpressionType.TableGet: {
                            if (expr.Name != null) {
                                Prg.AddVarGetInstruction(expr.Name);
                            } else {
                                ExprToBytecodeRecursive(expr.Right);
                            }

                            for (int i = 0; i < expr.Indices.Count; i++) {
                                ExprToBytecodeRecursive(expr.Indices[i]);
                                Prg.AddInstruction(op_tableget);
                            }

                            break;
                        }
                    case sws_ExpressionType.LuaCall: {
                            for (int i = expr.Arguments.Count - 1; i >= 0; i--) {
                                ExprToBytecodeRecursive(expr.Arguments[i]);
                            }

                            Prg.AddInstruction(op_closure, Prg.AddConst("lua " + expr.Name, sws_DataType.String));

                            Prg.AddInstruction(op_call, expr.Arguments.Count * (callReturnValues ? 1 : -1));
                            break;
                        }
                    case sws_ExpressionType.ClosureCall: {
                            if (expr.Left.Name == "_keys") {
                                if (expr.Arguments.Count != 1) {
                                    throw new sws_Error(Peek(), $"Expected 1 argument for '_keys', instead got {expr.Arguments.Count}.");
                                }

                                ExprToBytecodeRecursive(expr.Arguments[0]);
                                Prg.AddInstruction(op_tablekeys);

                                break;
                            } else if (expr.Left.Name == "_type") {
                                if (expr.Arguments.Count != 1) {
                                    throw new sws_Error(Peek(), $"Expected 1 argument for '_type', instead got {expr.Arguments.Count}.");
                                }

                                ExprToBytecodeRecursive(expr.Arguments[0]);
                                Prg.AddInstruction(op_type);

                                break;
                            }

                            for (int i = expr.Arguments.Count - 1; i >= 0; i--) {
                                ExprToBytecodeRecursive(expr.Arguments[i]);
                            }

                            ExprToBytecodeRecursive(expr.Left);

                            Prg.AddInstruction(op_call, expr.Arguments.Count * (callReturnValues ? 1 : -1));
                            break;
                        }
                    case sws_ExpressionType.SelfCall: {
                            throw new sws_Error(Peek(), "This type call is currently not implemented.");
                            break;
                        }
                    case sws_ExpressionType.Closure: {
                            if (expr.LuaClosure) {
                                Prg.AddVarGetInstruction("lua " + expr.Name);
                            } else {
                                Prg.AddVarGetInstruction(expr.Name);
                            }
                            break;
                        }
                }
            }

            void ExprToBytecodeOr(sws_Expression expr) {
                int flag = JMPFLAG + flagModCounter++;

                bool leftSide = (expr.Parent != null) && expr.Parent.Left == expr;

                ExprToBytecodeOrRecursive(expr);

                Prg.ResolveJumpFlag(flag, conditionalExpr ? 1 : 0);

                void ExprToBytecodeOrRecursive(sws_Expression e) {
                    if (e.Left.Op == operator_boolor) {
                        ExprToBytecodeOrRecursive(e.Left);
                    } else {
                        ExprToBytecodeRecursive(e.Left);
                    }

                    if (conditionalExpr) {
                        Prg.AddInstruction(op_jtrue, flag);
                        conditionalSkipJmpsExceptions.Add(Prg.GetPC());
                    } else {
                        Prg.AddInstruction(op_jtnp, flag);
                    }

                    ExprToBytecodeRecursive(e.Right);
                }
            }

            void ExprToBytecodeAnd(sws_Expression expr, bool childOfOr) {
                int flag = JMPFLAG + flagModCounter++;

                ExprToBytecodeAndRecursive(expr);

                Prg.ResolveJumpFlag(flag, childOfOr ? 1 : 0);

                void ExprToBytecodeAndRecursive(sws_Expression e) {
                    if(e.Left.Op == operator_booland) {
                        ExprToBytecodeAndRecursive(e.Left);
                    } else {
                        ExprToBytecodeRecursive(e.Left);
                    }

                    if (conditionalExpr || childOfOr) {
                        Prg.AddJumpFInstruction(flag);
                    } else {
                        Prg.AddInstruction(op_jfnp, flag);
                    }

                    ExprToBytecodeRecursive(e.Right);
                }
            }
        }

        /// <summary>
        /// See if next tokens are expression by running expression function and keeping track of starting conditions to revert in case of error. If the expression includes a closure it may even generate bytecode. If the error is one that would only happen if an actual expression was parsed, the errors are printed (the detection for this is very bad, luckly any errors will be caught after the return statement is parsed).
        /// </summary>
        /// <returns></returns>
        private static sws_Expression TryGetExpression() {
            sws_Error.Suppress = true;

            int currentTemp = current;

            try {
                sws_Error.Suppress = true;
                
                sws_Expression expr = Expression();
                
                sws_Error.Suppress = false;

                return expr;
            } catch(sws_Error e) {
                sws_Error.Suppress = false;
                if (e.Message != "expr_unexpected") {
                    throw new sws_Error(e);
                }
                current = currentTemp;
                return null;
            }
        }

        private static sws_Expression Expression() {
            //catch closures first (lua closures are handled in Primary())
            if(Match(keyword_func)) {
                string name = Prg.UnnammedFuncCount.ToString();
                
                ParseFunction(true);
                
                return new sws_Expression().Closure(name);
            }
            
            sws_Expression expr = BoolOr();

            return expr;
        }

        private static sws_Expression BoolOr() {
            sws_Expression expr = BoolAnd();

            while (Match(operator_boolor)) {
                sws_Expression right = BoolAnd();
                expr = new sws_Expression().Binary(expr, operator_boolor, right);
            }

            return expr;
        }

        private static sws_Expression BoolAnd() {
            sws_Expression expr = BitwiseOr();

            while (Match(operator_booland)) {
                sws_Expression right = BitwiseOr();
                expr = new sws_Expression().Binary(expr, operator_booland, right);
            }

            return expr;
        }

        private static sws_Expression BitwiseOr() {
            sws_Expression expr = BitwiseXor();

            while (Match(operator_bitor)) {
                sws_Expression right = BitwiseXor();
                expr = new sws_Expression().Binary(expr, operator_bitor, right);
            }

            return expr;
        }

        private static sws_Expression BitwiseXor() {
            sws_Expression expr = BitwiseAnd();

            while (Match(operator_bitxor)) {
                sws_Expression right = BitwiseAnd();
                expr = new sws_Expression().Binary(expr, operator_bitxor, right);
            }

            return expr;
        }

        private static sws_Expression BitwiseAnd() {
            sws_Expression expr = Equality();

            while (Match(operator_bitand)) {
                sws_Expression right = Equality();
                expr = new sws_Expression().Binary(expr, operator_bitand, right);
            }

            return expr;
        }

        private static sws_Expression Equality() {
            sws_Expression expr = Comparison();

            while (Match(new sws_TokenType[] { operator_eq, operator_neq })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Comparison();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private static sws_Expression Comparison() {
            sws_Expression expr = BitwiseShifts();

            while (Match(new sws_TokenType[] { operator_gt, operator_gte, operator_lt, operator_lte })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = BitwiseShifts();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private static sws_Expression BitwiseShifts() {
            sws_Expression expr = Term();

            while (Match(new sws_TokenType[] { operator_bitshiftleft, operator_bitshiftright })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Term();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private static sws_Expression Term() {
            sws_Expression expr = Factor();

            while (Match(new sws_TokenType[] { operator_add, operator_sub, operator_concat })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Factor();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private static sws_Expression Factor() {
            sws_Expression expr = Unary();

            while (Match(new sws_TokenType[] { operator_mul, operator_div, operator_floordiv, operator_mod })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Unary();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private static sws_Expression Unary() {
            if (Match(new sws_TokenType[] { operator_boolnot, operator_bitnot, operator_minus, operator_length, punctuation_doubleplus, punctuation_doubleminus })) {
                sws_TokenType op = Last().TokenType;
                if (op == punctuation_doubleplus) {
                    op = operator_prefixincrement;
                } else if (op == punctuation_doubleminus) {
                    op = operator_prefixdecrement;
                }
                sws_Expression right = Unary();
                return new sws_Expression().Unary(op, right);
            }

            return Exponent();
        }

        private static sws_Expression Exponent() {
            sws_Expression expr = Primary();

            while (Match(operator_pow)) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Exponent();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private static sws_Expression Primary() {
            if (Match(keyword_null)) {
                return new sws_Expression().Literal(null, sws_DataType.Null);
            }

            if (Match(keyword_true)) {
                return new sws_Expression().Literal(true, sws_DataType.Bool);
            }

            if (Match(keyword_false)) {
                return new sws_Expression().Literal(false, sws_DataType.Bool);
            }

            if (Match(literal_number)) {
                return new sws_Expression().Literal((double)Last().Literal, sws_DataType.Double);
            }

            if (Match(literal_string)) {
                return new sws_Expression().Literal(Last().Literal, sws_DataType.String);
            }

            //handles variables, table access, function calls, lua function calls, and postfix increments/decrements
            if (Match(identifier) || Match(keyword_lua)) {
                //stored as variable initially
                string name = Last().Literal.ToString();
                sws_Expression expr = new sws_Expression().Variable(name);

                //handle lua calls
                //in 'lua.input.getNumber()' the function name is stored as 'input getNumber' and so on.
                if(Last().TokenType == keyword_lua) {
                    name = string.Empty;

                    while(Peek().TokenType == punctuation_period) {
                        Consume(punctuation_period);
                        Consume(identifier, "Expected function name continued after '.'.");

                        name += Last().Literal.ToString();

                        if(Peek().TokenType == punctuation_period) {
                            name += " ";
                        }
                    }

                    //handle lua function closures
                    if (Peek().TokenType == punctuation_parenthesis_open) {
                        Consume(punctuation_parenthesis_open);
                        expr = new sws_Expression().LuaCall(name, ParseFuncArgs());
                    } else {
                        expr = new sws_Expression().Closure(name, true);
                        Prg.AddLuaClosure(name);
                    }
                }

                TableGet();

                //handle function calls
                if (Array.IndexOf(new sws_TokenType[] { identifier, punctuation_brackets_closed, punctuation_parenthesis_closed }, Last().TokenType) != -1 && Match(punctuation_parenthesis_open)) {
                    Call();
                }

                //handle postfix increment and decrement
                if (Array.IndexOf(new sws_TokenType[] { identifier, punctuation_brackets_closed }, Last().TokenType) != -1 && (Match(punctuation_doubleplus) || Match(punctuation_doubleminus))) {
                    IncDec();
                }

                //handle table get
                void TableGet() {
                    if (Peek().TokenType == punctuation_brackets_open || Peek().TokenType == punctuation_period || Peek().TokenType == punctuation_colon) {
                        List<sws_Expression> indices = new List<sws_Expression>();

                        //Parses 'data["a"]["b"]' and 'data.a.b' syntax for reading table values.
                        bool selfCall = Peek().TokenType == punctuation_colon;
                        Next();

                        if (Last().TokenType == punctuation_brackets_open) {
                            indices.Add(Expression());
                            Consume(punctuation_brackets_closed, "Expected ']' after table index.");
                        } else {
                            if (Peek().TokenType != identifier) {
                                throw new sws_Error(Peek(), $"Token type '{Peek().TokenType}' used as table index.");
                            }
                            indices.Add(new sws_Expression().Literal(Peek().Literal, sws_DataType.String));
                            Next();
                        }

                        if (!selfCall) {
                            expr = new sws_Expression().TableGet(expr, indices);
                            if (Match(punctuation_brackets_open) || Match(punctuation_period)) {
                                TableGet();
                            } else if (Match(punctuation_parenthesis_open)) {
                                Call();
                            }
                        } else {
                            Consume(punctuation_parenthesis_open, $"Expected table value to be called when using ':' syntax.");

                            Call(expr, indices.Last());
                        }
                    }
                }

                void Call(sws_Expression arg=null, sws_Expression funcName=null) {
                    List<sws_Expression> args = ParseFuncArgs();

                    if (arg != null) {
                        args.Insert(0, arg);
                        expr = new sws_Expression().SelfCall(expr, funcName, args);
                    } else {
                        expr = new sws_Expression().ClosureCall(expr, args);
                    }

                    //incase a call happens directly after a call (ex. 'test()()')
                    if (Array.IndexOf(new sws_TokenType[] { identifier, punctuation_brackets_closed, punctuation_parenthesis_closed }, Last().TokenType) != -1 && Match(punctuation_parenthesis_open)) {
                        Call();
                    } else {
                        TableGet();
                    }
                }

                void IncDec() {
                    expr = new sws_Expression().Unary(Last().TokenType == punctuation_doubleplus ? operator_postfixincrement : operator_postfixdecrement, expr);

                    if (Array.IndexOf(new sws_TokenType[] { identifier, punctuation_brackets_closed }, Last().TokenType) != -1 && (Match(punctuation_doubleplus) || Match(punctuation_doubleminus))) {
                        IncDec();
                    }
                }

                List<sws_Expression> ParseFuncArgs() {
                    List<sws_Expression> args = new List<sws_Expression>();

                    while (Peek().TokenType != punctuation_parenthesis_closed) {

                        args.Add(Expression());

                        if (Peek().TokenType != punctuation_parenthesis_closed) {
                            Consume(punctuation_comma, "Expected ',' between function call arguments.");

                            //case where comma is left after last argument (ex. 'test(2,4,)')
                            if (Peek().TokenType == punctuation_parenthesis_closed) {
                                throw new sws_Error(Peek(), "Expected function call argument after ','.");
                            }
                        }
                    }

                    //can't error due to while loop condition
                    Consume(punctuation_parenthesis_closed);

                    return args;
                }

                return expr;
            }

            //handle table declarations
            //syntax such as 'table = {test=3}' can be used.
            if (Match(punctuation_braces_open)) {
                List<sws_Expression> elementIndices = new List<sws_Expression>();
                List<sws_Expression> elements = new List<sws_Expression>();

                //for elements with index not specifically stated, start at 1 and increment by 1 for each. In 'table = {exampleIndex1 = 1, 2, exampleIndex2 = 3, 4}' 2 is at index 1 and 4 is at index 2.
                int indexCount = 1;

                while (Peek().TokenType != punctuation_braces_closed) {
                    //check for syntax to initilize element at specific index, uses index is a single token.
                    sws_Expression index = new sws_Expression().Literal((double)indexCount, sws_DataType.Double);
                    if (Peek2().TokenType == operator_assign) {
                        index = new sws_Expression().Literal(Peek());
                        Next(); //skip index
                        Next(); //skip '='
                    } else {
                        indexCount++;
                    }

                    sws_Expression element = Expression();

                    elementIndices.Add(index);
                    elements.Add(element);

                    if (Peek().TokenType != punctuation_braces_closed) {
                        Consume(punctuation_comma, "Expected ',' between table elements.");
                    }
                }

                Consume(punctuation_braces_closed);

                return new sws_Expression().Table(elementIndices, elements);
            }

            //handle parenthesis
            //treated as expressions within expressions (yay more recursion)
            if (Match(punctuation_parenthesis_open)) {
                sws_Expression expr = Expression();
                Consume(punctuation_parenthesis_closed, "Expected ')' at end of expression.");
                
                return expr;
            }

            throw new sws_Error(Peek(), $"Unexpected token '{Peek().TokenType}' in expression.", "expr_unexpected");
        }

        //helper functions for parser start here

        private static sws_Token Consume(sws_TokenType tokenType, string errorMessage = "") {
            if (Match(tokenType)) {
                return Last();
            }

            throw new sws_Error(Peek(), errorMessage);
        }

        private static bool Match(sws_TokenType tokenType) {
            if(AtEnd()) {
                return false;
            }
            if (Peek().TokenType == tokenType) {
                Next();
                return true;
            }
            return false;
        }

        private static bool Match(sws_TokenType[] tokenTypes) {
            if (AtEnd()) {
                return false;
            }
            if (Array.IndexOf(tokenTypes, Peek().TokenType) != -1) {
                Next();
                return true;
            }
            return false;
        }

        private static sws_Token Last() {
            if (current == 0) {
                return sws_Token.Error();
            }
            LastToken = Tokens[current - 1];
            return LastToken;
        }

        private static sws_Token Next() {
            if(AtEnd()) {
                return sws_Token.Error();
            }
            LastToken = Tokens[current++];
            return LastToken;
        }

        private static sws_Token Peek() {
            if(AtEnd()) {
                return sws_Token.Error();
            }
            LastToken = Tokens[current];
            return LastToken;
        }

        private static sws_Token Peek2() {
            if (current + 1 >= Tokens.Count) {
                return sws_Token.Error();
            }
            LastToken = Tokens[current + 1];
            return LastToken;
        }

        private static bool AtEnd() {
            return current >= Tokens.Count;
        }

        /// <summary>
        /// Skips tokens until reaching next decleration / statement.
        /// </summary>
        private static void Synchronize() {
            while (!AtEnd()) {
                switch (Peek().TokenType) {
                    case keyword_if: return;
                    case keyword_else: return;
                    case keyword_else_if: return;
                    case keyword_for: return;
                    case keyword_while: return;
                    case keyword_continue: return;
                    case keyword_break: return;
                    case keyword_func: return;
                    case keyword_return: return;
                }
                Next();
            }
        }

        /// <summary>
        /// Used as part of optimization for seeing if instructions like op_loadimm or op_addimm can be used. Checks if expression given is value which can be stored in 18 bit data section of instruction.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool ValidImmediateValue(sws_Expression value) {
            return  value.Type == sws_ExpressionType.Literal &&                         //check value is literal
                    value.ValueType == sws_DataType.Double &&                           //check literal is number
                    (double)value.Value % 1 == 0 &&                                     //check number is integer
                    (double)value.Value >= -131071 && (double)value.Value <= 131071;    //check if range is between -2^17+1 and 2^17-1
        }
    }
}
