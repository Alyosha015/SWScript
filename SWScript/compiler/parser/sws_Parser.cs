using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SWS.Compiler.sws_Common;

using static SWS.Compiler.sws_TokenType;
using static SWS.Compiler.sws_Opcode;

namespace SWS.Compiler {
    internal class sws_Parser {
        private SWScript _sws;
        private string _source;
        private string[] _sourceLines;
        private List<sws_Token> _tokens;

        /// <summary>
        /// Used as part of error handling, stores last token access with Next() / Peek() / etc.
        /// </summary>
        private sws_Token _lastToken;

        /// <summary>
        /// "_depth" of scope parser is currently in.
        /// </summary>
        private int _depth;

        /// <summary>
        /// Index of current token being read by parser.
        /// </summary>
        private int _current;

        /// <summary>
        /// Used to keep track of index of break and continue statement jump instructions at each depth.
        /// </summary>
        private Stack<List<int>> _continueBreakJumpLocations;

        private Stack<List<int>> _ifEndJumpLocations;

        private int _switchStatementIDCounter;

        #region Variables used to store compiled program

        private Dictionary<string, sws_Frame> _frames;

        private Stack<sws_Frame> _frameStack;

        private sws_Frame _frame;

        /// <summary>
        /// Used to name functions with no name (ex. 'local testfunc = func() {}'. Because you can't have a function with an integer as a name it should be fine.
        /// </summary>
        private int _unnammedFuncCount = 0;

        //globals and constants are shared between all frames. Due to bytecode format they are both limited to 2^17 (131072) each.
        private List<sws_Variable> _globals;
        private List<sws_Variable> _constants;

        #region Property Sets

        private Dictionary<string, string> _propertyTexts;
        private Dictionary<string, double> _propertyNumbers;
        private Dictionary<string, bool> _propertyBools;

        #endregion
        #endregion

        public sws_Program Parse(SWScript sws, string source, string[] sourceLines, List<sws_Token> tokens) {
            _sws = sws;
            _source = source;
            _sourceLines = sourceLines;
            _tokens = tokens;

            _current = 0;
            _depth = 0;

            _continueBreakJumpLocations = new Stack<List<int>>();
            _ifEndJumpLocations = new Stack<List<int>>();

            _frames = new Dictionary<string, sws_Frame>();
            _frameStack = new Stack<sws_Frame>();
            _globals = new List<sws_Variable>();
            _constants = new List<sws_Variable>();

            _propertyTexts = new Dictionary<string, string>();
            _propertyNumbers = new Dictionary<string, double>();
            _propertyBools = new Dictionary<string, bool>();

            _lastToken = sws_Token.Error();

            PushFrame(string.Empty, true);

            while (!AtEnd()) {
                try {
                    ParserMain();
                } catch (sws_ParserError e) {
                    if (e.Token == null) {
                        e.Token = _lastToken;
                    }

                    int line = e.Token.NLine, col = e.Token.NColumn;

                    if(line == -1) {
                        line = sourceLines.Length;
                    }

                    if(col == -1) {
                        col = 0;
                    }

                    _sws.MessageAction(MsgType.ErrCompile, line, col, sourceLines[line - 1], e.ErrMessage);
                    return null;
                }
            }

            _frames[string.Empty].Program.Add(new sws_Op(op_halt, int.MinValue, _lastToken.NLine));

            SetLocalScopeEnd();

            return new sws_Program(_source, _sourceLines, _tokens, _frames, _globals, _constants, _propertyTexts, _propertyNumbers, _propertyBools);
        }

        private void HandleWarning(sws_Warning warning) {
            if (warning.Token == null) {
                warning.Token = _lastToken;
            }

            _sws.MessageAction(MsgType.Warning, warning.Token.NLine, warning.Token.NColumn, _sourceLines[warning.Token.NLine - 1], warning.ErrMessage);
        }

        //parser functions start here

        private void ParserMain() {
            if (Match(keyword_func)) {
                ParseFunction();
            } else if (Match(keyword_if)) {
                IfStatement();
            } else if (Match(keyword_for)) {
                _depth++;
                _continueBreakJumpLocations.Push(new List<int>());
                ForStatement();
                _continueBreakJumpLocations.Pop();
                _depth--;
            } else if (Match(keyword_while)) {
                _depth++;
                _continueBreakJumpLocations.Push(new List<int>());
                WhileStatement();
                _continueBreakJumpLocations.Pop();
                _depth--;
            } else if (Match(keyword_continue)) {
                AddInstruction(op_jmp, JMP_CONTINUE);
                _continueBreakJumpLocations.Peek().Add(GetPC());
            } else if (Match(keyword_break)) {
                AddInstruction(op_jmp, JMP_BREAK);
                _continueBreakJumpLocations.Peek().Add(GetPC());
            } else if (Match(keyword_intswitch)) {
                IntSwitchStatement();
            } else if (Match(keyword_switch)) {
                SwitchStatement();
            } else if (Match(keyword_return)) {
                ReturnStatement();
            } else if (Peek().TokenType == keyword_print || Peek().TokenType == keyword_println) {
                bool println = Peek().TokenType == keyword_println;
                Next();
                PrintStatement(println);
            } else if (Match(keyword_property)) {
                SetPropertyStatement();
            } else if ((Peek().TokenType == identifier && Peek2().TokenType != punctuation_parenthesis_open) || Peek().TokenType == keyword_local) {
                sws_Statement statement = ExpressionStatement(true);
                if (statement != null) {
                    StatementToBytecode(statement);
                }
            } else if (Peek().TokenType == identifier || Peek().TokenType == keyword_lua) { //most function calls generated here
                ExprToBytecode(Expression(), true, false);
            } else if (Match(punctuation_braces_open)) {
                _depth++;

                Body();

                SetLocalScopeEnd();
                _depth--;
            } else {
                throw new sws_ParserError(Peek(), $"Unexpected token '{Peek().TokenType}'");
            }
        }

        private void ParseFunction(bool expectNoName = false) {
            if (expectNoName && Peek().TokenType != punctuation_parenthesis_open) {
                if(Peek().TokenType == identifier) {
                    throw new sws_ParserError(Peek(), "A function in an expression can't have a name. Expected 'func()'.");
                } else {
                    throw new sws_ParserError(Peek(), "Expected '('.");
                }
            }

            //parse function name and push new frame for function
            if (Peek().TokenType != punctuation_parenthesis_open) {
                Consume(identifier, "Expected function name.");
                PushFrame(Last().Literal.ToString(), _depth < 1);
            } else {
                PushNoName();
            }

            _depth++;

            //parse function arguments.
            Consume(punctuation_parenthesis_open, "Expected '('.");

            List<string> parameters = new List<string>();
            while(Peek().TokenType != punctuation_parenthesis_closed) {
                parameters.Add(Consume(identifier, "Expected variable in function parameters").Literal.ToString());

                if(Peek().TokenType != punctuation_parenthesis_closed) {
                    Consume(punctuation_comma, "Expected ',' between function parameters.");

                    if (Peek().TokenType == punctuation_parenthesis_closed) {
                        throw new sws_ParserError(Peek(), "Expected function parameter after ','.");
                    }
                }
            }

            Consume(punctuation_parenthesis_closed, "Expected ')'.");

            for (int i = 0; i < parameters.Count; i++) {
                AddVariable(parameters[i], sws_VariableType.Local);
            }

            //parse function body
            Consume(punctuation_braces_open, "Expected '{' to begin function body.");
            SetLocalScopeStart();

            while (!AtEnd() && (Peek().TokenType != punctuation_braces_closed)) {
                ParserMain();
            }

            SetLocalScopeEnd();
            Consume(punctuation_braces_closed, "Expected '}' to end function body.");

            //adds return statement if there's none
            if (_frame.Program.Count == 0 || _frame.Program.Last().Opcode != op_return) {
                AddInstruction(op_return, 0);
            }

            PopFrame();

            _depth--;
        }

        private void IfStatement() {
            int pc;
            _ifEndJumpLocations.Push(new List<int>());

            sws_Expression condition = Expression();
            List<int> jumpsToResolve = ExprToBytecode(condition, true, true, true);
            _depth++;

            AddJumpFInstruction(JMP_IFEND);
            pc = GetPC();

            Consume(punctuation_braces_open, "Expected '{'.");

            Body();
            SetLocalScopeEnd();

            //jumps to end of if-else if-else chain, only add if it's not the last
            if (Peek().TokenType == keyword_else_if || Peek().TokenType == keyword_else) {
                AddInstruction(op_jmp, JMP_IFEND);
                _ifEndJumpLocations.Peek().Add(GetPC());
            }

            ResolveJumpPC(pc);
            ResolveJumpPCs(jumpsToResolve);

            _depth--;

            while(Match(keyword_else_if)) {
                sws_Expression ifElseCondition = Expression();
                jumpsToResolve = ExprToBytecode(ifElseCondition, true, true, true);

                _depth++;

                AddJumpFInstruction(JMP_IFEND);
                pc = GetPC();

                Consume(punctuation_braces_open, "Expected '{'.");

                Body();
                SetLocalScopeEnd();

                //jumps to end of if-else if-else chain, only add if it's not the last
                if(Peek().TokenType == keyword_else_if || Peek().TokenType == keyword_else) {
                    AddInstruction(op_jmp, JMP_IFEND);
                    _ifEndJumpLocations.Peek().Add(GetPC());
                }

                ResolveJumpPC(pc);
                ResolveJumpPCs(jumpsToResolve);

                _depth--;
            }

            if (Match(keyword_else)) {
                Consume(punctuation_braces_open, "Expected '{'.");

                _depth++;

                Body();
                SetLocalScopeEnd();

                _depth--;
            }

            ResolveIfEnd(_ifEndJumpLocations.Peek(), GetPC());

            _ifEndJumpLocations.Pop();
        }

        /// <summary>
        /// Parses for statement. A for can be formated two ways: 'for a, b {}' and 'for a, b, c {}'. a and b are identical for both, with a creating the iterator variable (<name>=<expression>), and b being the condition expression for the loop. In the second type, however, c is an expression which executes at the end of each loop. If c is not present the variable in a is declared instead.
        /// </summary>
        private void ForStatement() {
            //a
            Consume(identifier, "Expected identifer for iterator variable's name.");

            string iterator = Last().Literal.ToString();

            Consume(operator_assign, "Expected '='.");
            
            sws_Expression iteratorInitializer = Expression();

            AddVariable(iterator, sws_VariableType.Local);
            ExprToBytecode(iteratorInitializer);
            SetLocalScopeStart();
            AddVarSetInstruction(iterator);

            //b

            Consume(punctuation_comma, "Expected ','.");

            sws_Expression condition = Expression();

            //c
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
            int conditionStartPc = GetPC();
            List<int> jumpsToResolve = ExprToBytecode(condition, true, true, true);
            AddJumpFInstruction(JMP_CONDITION);
            int jfpc = GetPC();

            Body();

            //logic for incrementing and looping back to checking condition
            int incrementStartPC = GetPC();

            if (!hasCTerm) {
                AddVarGetInstruction(iterator);
                AddInstruction(op_addimm, 1);
                AddVarSetInstruction(iterator);
            } else {
                StatementToBytecode(statement);
            }

            AddInstruction(op_jmp, conditionStartPc - GetPC() - 1);

            //

            ResolveJumpPC(jfpc);
            ResolveJumpPCs(jumpsToResolve);

            ResolveContinueBreakLoop(_continueBreakJumpLocations.Peek(), incrementStartPC, GetPC());

            SetLocalScopeEnd();
        }

        private void WhileStatement() {
            sws_Expression condition = Expression();

            Consume(punctuation_braces_open, "Expected '{'.");

            int conditionStartPc = GetPC();
            List<int> jumpsToResolve = ExprToBytecode(condition, true, true, true);
            AddJumpFInstruction(JMP_CONDITION);
            int jfpc = GetPC();

            Body();

            AddInstruction(op_jmp, conditionStartPc - GetPC() - 1);

            //

            ResolveJumpPC(jfpc);
            ResolveJumpPCs(jumpsToResolve);

            ResolveContinueBreakLoop(_continueBreakJumpLocations.Peek(), conditionStartPc, GetPC());

            SetLocalScopeEnd();
        }

        private void IntSwitchStatement() {
            HandleWarning(new sws_Warning(Peek(), "switch is recommended over intswitch, it's faster and isn't limited to numbers."));

            sws_Expression expr = Expression();
            
            Consume(punctuation_braces_open, "Expected '{' after intswitch expression.");

            string tempVarName = $"sws_intswitch_temp {_switchStatementIDCounter++}";

            // * * * * Bounds Check * * * *
            AddVariable(tempVarName, sws_VariableType.Global);

            int loadMaxPC = AddInstruction(op_loadimm);

            ExprToBytecode(expr);
            AddInstruction(op_dup); //used for upper bound check
            AddVarSetInstruction(tempVarName);

            //upper bound check
            int maxBoundJumpPC = AddInstruction(op_jgt); //jumps to default block

            //lower bound check
            AddVarGetInstruction(tempVarName);
            AddInstruction(op_dup);
            int loadMinPC = AddInstruction(op_loadimm);
            int minBoundJumpPC = AddInstruction(op_jgt); //jumps to default block
            // * * * * * * * *

            int indexedJumpPC = AddInstruction(op_jindexed);

            Dictionary<int, int> caseBodyPCs = new Dictionary<int, int>();
            List<int> caseExitPCs = new List<int>();
            int min = int.MaxValue;
            int max = int.MinValue;

            //parse case statements
            while(Match(keyword_case)) {
                _depth++;

                sws_Expression @case = Expression();
                @case.ConstantFold();

                if(@case.Type != sws_ExpressionType.Literal) {
                    throw new sws_ParserError(Peek(), $"Expected case expression to be literal. Current type: '{@case.Type}'.");
                }

                if(@case.ValueType != sws_DataType.Double) {
                    throw new sws_ParserError(Peek(), $"Expected case expression to be number. Current type: '{@case.ValueType}'.");
                }

                double value = (double)@case.Value;
                if(value % 1 != 0 && value < 0) {
                    throw new sws_ParserError(Peek(), $"intswitch case must be integer and positive, instead got '{value}'.");
                }

                if(value < min) {
                    min = (int)value;
                }

                if(value > max) {
                    max = (int)value;
                }

                Consume(punctuation_braces_open, "Expected '{' after case expression.");

                caseBodyPCs.Add((int)value, GetPC() + 1);

                Body();

                caseExitPCs.Add(AddInstruction(op_jmp));
                
                SetLocalScopeEnd();
                _depth--;
            }

            _frame.Program[indexedJumpPC].Data = -min;
            _frame.Program[loadMinPC].Data = min;
            _frame.Program[loadMaxPC].Data = max;

            int range = max - min;
            if(range > 256) {
                HandleWarning(new sws_Warning(Peek(), $"intswitch has very large range ({range}). This may lead to large program size."));
            }

            //create jump table
            List<int> jumpsToDefaultBlock = new List<int>();
            List<int> jumpsToCaseBlock = new List<int>();

            for (int i = min; i < max + 1; i++) {
                InsertInstruction(indexedJumpPC + i - min + 1, op_jmp);
                if (!caseBodyPCs.ContainsKey(i)) {
                    jumpsToDefaultBlock.Add(indexedJumpPC + i - min + 1);
                } else {
                    jumpsToCaseBlock.Add(indexedJumpPC + i - min + 1);
                }
            }

            //resolve jumps to case blocks
            List<int> cases = caseBodyPCs.Values.ToList();
            for (int i = 0; i < cases.Count; i++) {
                ResolveJumpPcCustomDest(jumpsToCaseBlock[i], cases[i] + range);
            }

            // * * * * Default Statement * * * *

            Consume(keyword_default, "Expected 'default' statement.");
            Consume(punctuation_braces_open, "Expected '{' after 'default'.");

            //resolve jumps to default block
            ResolveJumpPC(maxBoundJumpPC);
            ResolveJumpPC(minBoundJumpPC);
            ResolveJumpPCs(jumpsToDefaultBlock);

            _depth++;

            Body();

            SetLocalScopeEnd();
            _depth--;

            // * * * * * * * *

            ResolveJumpPCs(caseExitPCs, range + 1);

            //if this ever happens I'll be impressed.
            if (GetPC() - indexedJumpPC > 131071) {
                throw new sws_ParserError(_lastToken, $"intswitch statement size too large.");
            }

            Consume(punctuation_braces_closed, "Expected '}' to end intswitch statement.");
        }

        private void SwitchStatement() {
            List<sws_Expression> map = new List<sws_Expression>(); //stores literals used in case <expr>

            sws_Expression expr = Expression();

            Consume(punctuation_braces_open, "Expected '{' after switch expression.");

            int mapGetPC = AddInstruction(op_getconst);
            ExprToBytecode(expr);
            AddInstruction(op_tableget);
            AddInstruction(op_dup);
            AddInstruction(op_loadnull);
            int jumpToDefault = AddInstruction(op_jeq); //jumps to default block. Note that there would be a null on the stack which needs to be cleared, so there will be a bit of code to that before the default block.

            int indexedJumpPC = AddInstruction(op_jindexed, 0);

            List<int> caseBodyPCs = new List<int>();
            List<int> caseExitPCs = new List<int>();

            //parse case statements
            while (Match(keyword_case)) {
                _depth++;

                sws_Expression @case = Expression();
                @case.ConstantFold();

                if (@case.Type != sws_ExpressionType.Literal) {
                    throw new sws_ParserError(Peek(), "Expected case expression to be literal.");
                }

                if (@case.ValueType == sws_DataType.Function || @case.ValueType == sws_DataType.Table || @case.ValueType == sws_DataType.Null) {
                    throw new sws_ParserError(Peek(), "Case can't be table, function, or null.");
                }

                map.Add(@case);

                Consume(punctuation_braces_open, "Expected '{' after case expression.");

                caseBodyPCs.Add(GetPC() + 1);

                Body();

                caseExitPCs.Add(AddInstruction(op_jmp));

                SetLocalScopeEnd();
                _depth--;
            }

            //create jump table
            sws_Table swsTable = new sws_Table();
            Dictionary<sws_Variable, sws_Variable> table = swsTable.Table;
            for (int i = caseBodyPCs.Count - 1; i >= 0; i--) {
                int jumpOffset = caseBodyPCs[i] - indexedJumpPC - 1;

                table.Add(new sws_Variable().Constant(map[i].Value, map[i].ValueType, -1), new sws_Variable().Constant((double)jumpOffset, sws_DataType.Double, -1));
            }
            int mapConstIndex = AddConst(swsTable, sws_DataType.Table);
            _frame.Program[mapGetPC].Data = mapConstIndex;

            //clear null from stack (extra value on stack for indexed jump isn't used)
            ResolveJumpPC(jumpToDefault);
            AddInstruction(op_loadnull);
            AddInstruction(op_jeq, 0);

            // * * * * Default Statement * * * *

            Consume(keyword_default, "Expected 'default' statement.");
            Consume(punctuation_braces_open, "Expected '{' after 'default'.");

            _depth++;

            Body();

            SetLocalScopeEnd();
            _depth--;

            // * * * * * * * *

            ResolveJumpPCs(caseExitPCs);

            Consume(punctuation_braces_closed, "Expected '}' to end switch statement.");
        }

        private void ReturnStatement() {
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

            AddInstruction(op_return, returnValuesCount);
        }

        private void PrintStatement(bool println) {
            Consume(punctuation_parenthesis_open, "Expected '(' after print/println.");

            //print statement with no arguments. (just give it an empty string instead).
            if (Match(punctuation_parenthesis_closed)) {
                AddInstruction(op_getconst, AddConst(string.Empty, sws_DataType.String));
                AddInstruction(op_print, println ? 0 : 1);
                return;
            }

            sws_Expression expr = Expression();
            ExprToBytecode(expr);

            AddInstruction(op_print, println ? 0 : 1);

            Consume(punctuation_parenthesis_closed, "Expected ')' after print/println function argument.");
        }

        private void SetPropertyStatement() {
            sws_Expression typeExpr = Expression();

            if (!typeExpr.IsType(sws_ExpressionType.Variable)) {
                throw new sws_ParserError("Expected property type to be name.");
            }

            Consume(punctuation_comma, "Expected ','.");
            sws_Expression nameExpr = Expression();
            if (!nameExpr.IsType(sws_ExpressionType.Literal)) {
                throw new sws_ParserError("Expected property name to be literal.");
            }

            Consume(punctuation_comma, "Expected ','.");
            sws_Expression valueExpr = Expression();
            if (!valueExpr.IsType(sws_ExpressionType.Literal)) {
                throw new sws_ParserError("Expected property value to be literal.");
            }

            string type = typeExpr.Name;
            string name = nameExpr.Value.ToString();
            string valueStr = valueExpr.Value.ToString();

            switch (type) {
                case "text": {
                        if(_propertyTexts.ContainsKey(name)) {
                            HandleWarning(new sws_Warning(Last(), $"Overwritten property text '{name}'."));
                            _propertyTexts[name] = valueStr;
                        } else {
                            _propertyTexts.Add(name, valueStr);
                        }
                        break;
                    }
                case "number": {
                        if(!double.TryParse(valueStr, out double value)) {
                            throw new sws_ParserError($"Unable to parse value '{valueStr}' to double.");
                        }

                        if (_propertyNumbers.ContainsKey(name)) {
                            HandleWarning(new sws_Warning(Last(), $"Overwritten property number '{name}'."));
                            _propertyNumbers[name] = value;
                        } else {
                            _propertyNumbers.Add(name, value);
                        }
                        break;
                    }
                case "bool": {
                        if (!bool.TryParse(valueStr, out bool value)) {
                            throw new sws_ParserError($"Unable to parse value '{valueStr}' to bool.");
                        }

                        if (_propertyBools.ContainsKey(name)) {
                            HandleWarning(new sws_Warning(Last(), $"Overwritten property bool '{name}'."));
                            _propertyBools[name] = value;
                        } else {
                            _propertyBools.Add(name, value);
                        }
                        break;
                    }
                default: throw new sws_ParserError($"Expected type of property box to be 'text', 'number', or 'bool', not '{type}'.");
            }
        }

        private void Body() {
            //NOTE: Doesn't close local variable scope, don't remember why I made it like this.
            while (!Match(punctuation_braces_closed)) {
                ParserMain();
            }
        }

        /// <summary>
        /// Variable assignment and table set statements. This is complicated by the fact that there can be multiple assignments in a single statement, for example 'a,b=b,a' to swap variables. Additionally, operators such as += can also be used, even with multiple variables. When there are multiple variables used the same amount is expected on the other side, but if there's less the rest are filled in with nulls. This is with the exception of function calls which can return multiple variables, but in that case only one function call and no other expressions are allowed because I don't want to deal with it. Additionally, this code handles increment/decrement statements. I would also like to apologize in advance for the spaghetti code, there are way too many combinations of these conditions. (This code is in the function StatementToBytecode()).
        /// 
        /// This function was split up from StatementToBytecode so that bytecode could be generated after a statement was parsed, which is used when parsing for loops for the third part of the statement, which needs to be parsed before the body and generated afterward.
        /// </summary>
        private sws_Statement ExpressionStatement(bool generateBytecode) {
            bool local = Match(keyword_local);

            List<sws_Expression> variables = new List<sws_Expression>(); //left side of assignment
            List<sws_Expression> expressions = new List<sws_Expression>(); //right side of assignment

            sws_TokenType assignmentOperator;

            while (true) {
                variables.Add(Expression());

                //check for x++ type statements and function calls
                if (variables.Count == 1) {
                    sws_TokenType op = variables[0].Op;

                    if (op == operator_prefixincrement || op == operator_prefixdecrement || op == operator_postfixincrement || op == operator_postfixdecrement) {
                        if (generateBytecode) {
                            ExprToBytecode(variables[0], false);
                            return null;
                        } else {
                            return new sws_Statement(sws_StatementType.IncDec, null, EOF, variables, local);
                        }
                    }

                    //function calls
                    if (variables[0].Type == sws_ExpressionType.ClosureCall) {
                        if (generateBytecode) {
                            ExprToBytecode(variables[0], true, false);
                            return null;
                        } else {
                            return new sws_Statement(sws_StatementType.Call, null, EOF, variables, local);
                        }
                    }

                    //self function calls
                    if (variables[0].Type == sws_ExpressionType.SelfCall) {
                        if (generateBytecode) {
                            ExprToBytecode(variables[0], true, false);
                            return null;
                        } else {
                            return new sws_Statement(sws_StatementType.SelfCall, null, EOF, variables, local);
                        }
                    }
                }

                if(!Match(punctuation_comma)) {
                    break;
                }
            }

            sws_Token assignmentOpToken = Peek();

            if(assignmentOpToken.IsAssignmentOperator()) {
                Next();

                assignmentOperator = assignmentOpToken.TokenType;

                //prevent code like 'local a += 10', which isn't possible
                if (local && assignmentOperator != operator_assign) {
                    throw new sws_ParserError(Last(), $"Can't use '{assignmentOperator}' when declaring local variable. Use '=' instead.");
                }

                while (true) {
                    expressions.Add(Expression());

                    if (!Match(punctuation_comma)) {
                        break;
                    }
                }

            } else if (!local) {
                throw new sws_ParserError(Last(), "Expected assignment operator in statement. Only local variables can be declared without an assignment operator. (ex. 'local a, b')");
            } else {
                //used when there is no assignment operator
                assignmentOperator = EOF;
            }

            return new sws_Statement(sws_StatementType.Normal, expressions, assignmentOperator, variables, local);
        }

        private void StatementToBytecode(sws_Statement statement) {

            sws_StatementType type = statement.Type;
            List<sws_Expression> expressions = statement.Expressions;
            sws_TokenType assignmentOperator = statement.AssignmentOperator;
            List<sws_Expression> variables = statement.Variables;
            bool local = statement.Local;

            if(assignmentOperator == EOF && type != sws_StatementType.IncDec) {
                for (int i = 0; i < variables.Count; i++) {
                    expressions.Add(new sws_Expression().Literal(null, sws_DataType.Null));
                }
                assignmentOperator = operator_assign;
            }

            if (type == sws_StatementType.IncDec) {
                ExprToBytecode(variables[0], false);
                return;
            } else if (type == sws_StatementType.Call) {
                ExprToBytecode(variables[0], true, false);
                return;
            } else if (type == sws_StatementType.SelfCall) {
                ExprToBytecode(variables[0], true, false);
                return;
            }

            bool functionCallSpecialCase = expressions.Count == 1 && (expressions[0].Type == sws_ExpressionType.LuaCall || expressions[0].Type == sws_ExpressionType.ClosureCall || expressions[0].Type == sws_ExpressionType.SelfCall);

            if (variables.Count < expressions.Count) {
                throw new sws_ParserError(Last(), $"Number of variables on left ({variables.Count}) less than number of expressions on right ({expressions.Count}).");
            }

            if (!functionCallSpecialCase) {
                //fills in right side in cases like 'a, b = 20'. Becomes equivalent to 'a, b = 20, null'.
                for (int i = expressions.Count; i < variables.Count; i++) {
                    expressions.Add(new sws_Expression().Literal(null, sws_DataType.Null));
                }

                for (int i = 0; i < expressions.Count; i++) {
                    sws_Expression variable = variables[i];
                    sws_Expression expr = expressions[i];

                    if(expr.Type == sws_ExpressionType.ClosureCall || expr.Type == sws_ExpressionType.LuaCall || expr.Type == sws_ExpressionType.SelfCall) {
                        expr.CallValuesReturned = 1;
                    }

                    bool table = variable.Type == sws_ExpressionType.TableGet;

                    //left side, ensure variables exist
                    AddVariable(variable.Name, local ? sws_VariableType.Local : 0);

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
                        AddInstruction(op_tableset, 1);
                    } else {
                        AddVarSetInstruction(variable.Name);
                    }
                }
            } else {
                expressions[0].CallValuesReturned = variables.Count;
                //left side, ensure variables exist
                for (int i = 0; i < variables.Count; i++) {
                    AddVariable(variables[i].Name, local ? sws_VariableType.Local : 0);
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
                        AddInstruction(op_tableset, 1);
                    } else {
                        AddVarSetInstruction(variable.Name);
                    }
                }
            }

            SetLocalScopeStart();

            void AssignOperatorCase(sws_Expression variable, sws_Expression expression) {
                ExprToBytecode(variable);

                switch (assignmentOperator) {
                    case operator_add_assign: AddInstruction(op_add); break;
                    case operator_sub_assign: AddInstruction(op_sub); break;
                    case operator_mul_assign: AddInstruction(op_mul); break;
                    case operator_div_assign: AddInstruction(op_div); break;
                    case operator_floordiv_assign: AddInstruction(op_floordiv); break;
                    case operator_pow_assign: AddInstruction(op_pow); break;
                    case operator_mod_assign: AddInstruction(op_mod); break;
                    case operator_booland_assign: {
                            AddInstruction(op_jfnp);
                            int pc = GetPC();
                            
                            ExprToBytecode(expression);

                            ResolveJumpPC(pc);
                            break;
                        }
                    case operator_boolor_assign: {
                            AddInstruction(op_jtnp);
                            int pc = GetPC();

                            ExprToBytecode(expression);
                            
                            ResolveJumpPC(pc);
                            break;
                        }
                    case operator_bitand_assign: AddInstruction(op_bitand); break;
                    case operator_bitxor_assign: AddInstruction(op_bitxor); break;
                    case operator_bitor_assign: AddInstruction(op_bitor); break;
                    case operator_bitshiftleft_assign: AddInstruction(op_bitshiftleft); break;
                    case operator_bitshiftright_assign: AddInstruction(op_bitshiftright); break;
                    case operator_concat_assign: AddInstruction(op_concat, 0); break;
                }
            }
        }

        //Similar to regular table get, but doesn't get the last index. This is primarly used to set tables in expression statements.
        private void TableGetNoLastIndex(sws_Expression variable) {
            AddVarGetInstruction(variable.Name);

            for (int i = 0; i < variable.Indices.Count - 1; i++) {
                ExprToBytecode(variable.Indices[i]);
                AddInstruction(op_tableget);
            }
        }

        //functions for parsing expressions. Only ExprToBytecode, TryGetExpression, and Expression are called by other parts of the parser. The remaining functions are a part of a recursive descent parser for expressions.

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="incDecReturnValues"></param>
        /// <param name="callReturnValues"></param>
        /// <param name="conditionalExpr"></param>
        /// <returns>List of pc indexes for instructions of a conditional expression which jump away when it evaluates false. These are later resolved by the parser to make them skip a block of code, which can't be done now as that code hasn't been parsed yet.</returns>
        /// <exception cref="sws_ParserError"></exception>
        private List<int> ExprToBytecode(sws_Expression expression, bool incDecReturnValues = true, bool callReturnValues = true, bool conditionalExpr = false) {
            int flagModCounter = 0;

            int pcStart = GetPC();
            pcStart = Math.Max(0, pcStart);

            List<int> conditionalSkipJmpsPCs = new List<int>();
            List<int> conditionalSkipJmpsExceptions = new List<int>();

            expression = expression.ConstantFold();

            bool functionCallParent = expression.Type == sws_ExpressionType.ClosureCall || expression.Type == sws_ExpressionType.LuaCall || expression.Type == sws_ExpressionType.SelfCall; //NOTE: gets set false in ExprToBytecodeRecursive if true.
            ExprToBytecodeRecursive(expression);

            if (conditionalExpr) {
                for (int i = pcStart; i < _frame.Program.Count; i++) {                    
                    sws_Op opcode = _frame.Program[i];

                    if (opcode.IsJumpOp() && (i + opcode.Data - _frame.Program.Count == -1) && !conditionalSkipJmpsExceptions.Contains(i)) {
                        conditionalSkipJmpsPCs.Add(i);
                    }
                }
            }

            bool jfalseCanSimplify = JumpFCanSimplify();

            if (jfalseCanSimplify) {
                for (int i = pcStart; i < _frame.Program.Count; i++) {
                    sws_Op opcode = _frame.Program[i];

                    if (opcode.IsJumpOp() && (i + opcode.Data - _frame.Program.Count == 0)) {
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
                                case operator_boolnot: AddInstruction(op_boolnot); break;
                                case operator_bitnot: AddInstruction(op_bitnot); break;
                                case operator_minus: AddInstruction(op_minus); break;
                                case operator_length: AddInstruction(op_len); break;
                                case operator_prefixincrement: {
                                        bool table = expr.Right.Type == sws_ExpressionType.TableGet;

                                        ExprToBytecodeRecursive(expr.Right);

                                        AddInstruction(op_addimm, 1);

                                        if (incDecReturnValues) {
                                            AddInstruction(op_dup);
                                        }

                                        if (table) {
                                            ExprToBytecodeRecursive(expr.Right.Indices.Last());
                                            TableGetNoLastIndex(expr.Right);
                                            AddInstruction(op_tableset, 1);
                                        } else {
                                            AddVarSetInstruction(expr.Right.Name);
                                        }

                                        break;
                                    }
                                case operator_prefixdecrement: {
                                        bool table = expr.Right.Type == sws_ExpressionType.TableGet;

                                        ExprToBytecodeRecursive(expr.Right);

                                        AddInstruction(op_addimm, -1);

                                        if (incDecReturnValues) {
                                            AddInstruction(op_dup);
                                        }

                                        if (table) {
                                            ExprToBytecodeRecursive(expr.Right.Indices.Last());
                                            TableGetNoLastIndex(expr.Right);
                                            AddInstruction(op_tableset, 1);
                                        } else {
                                            AddVarSetInstruction(expr.Right.Name);
                                        }

                                        break;
                                    }
                                case operator_postfixincrement: {
                                        bool table = expr.Right.Type == sws_ExpressionType.TableGet;

                                        ExprToBytecodeRecursive(expr.Right);

                                        if(incDecReturnValues) {
                                            AddInstruction(op_dup);
                                        }

                                        AddInstruction(op_addimm, 1);

                                        if (table) {
                                            ExprToBytecodeRecursive(expr.Right.Indices.Last());
                                            TableGetNoLastIndex(expr.Right);
                                            AddInstruction(op_tableset, 1);
                                        } else {
                                            AddVarSetInstruction(expr.Right.Name);
                                        }

                                        break;
                                    }
                                case operator_postfixdecrement: {
                                        bool table = expr.Right.Type == sws_ExpressionType.TableGet;

                                        ExprToBytecodeRecursive(expr.Right);

                                        if (incDecReturnValues) {
                                            AddInstruction(op_dup);
                                        }

                                        AddInstruction(op_addimm, -1);

                                        if (table) {
                                            ExprToBytecodeRecursive(expr.Right.Indices.Last());
                                            TableGetNoLastIndex(expr.Right);
                                            AddInstruction(op_tableset, 1);
                                        } else {
                                            AddVarSetInstruction(expr.Right.Name);
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
                                case operator_boolor: ExprToBytecodeOr(expr); break;
                                case operator_booland: {
                                        ExprToBytecodeAnd(expr, (expr.Parent != null && expr.Parent.Op == operator_booland && expr.Parent.Left == expr) || (expr.Parent != null && expr.Parent.Parent != null && expr.Parent.Parent.Op == operator_booland && expr.Parent.Parent.Right.Left == expr));
                                        break;
                                    }
                                case operator_bitor: AddInstruction(op_bitand); break;
                                case operator_bitxor: AddInstruction(op_bitxor); break;
                                case operator_bitand: AddInstruction(op_bitand); break;
                                case operator_eq: AddInstruction(op_eq); break;
                                case operator_neq: AddInstruction(op_neq); break;
                                case operator_gt: AddInstruction(op_lt); break;
                                case operator_gte: AddInstruction(op_lte); break;
                                case operator_lt: AddInstruction(op_lt); break;
                                case operator_lte: AddInstruction(op_lte); break;
                                case operator_bitshiftleft: AddInstruction(op_bitshiftleft); break;
                                case operator_bitshiftright: AddInstruction(op_bitshiftright); break;
                                case operator_add: AddInstruction(op_add); break;
                                case operator_sub: AddInstruction(op_sub); break;
                                case operator_mul: AddInstruction(op_mul); break;
                                case operator_div: AddInstruction(op_div); break;
                                case operator_floordiv: AddInstruction(op_floordiv); break;
                                case operator_mod: AddInstruction(op_mod); break;
                                case operator_pow: AddInstruction(op_pow); break;
                                case operator_concat: AddInstruction(op_concat, 0); break;
                            }
                            break;
                        }
                    case sws_ExpressionType.Literal: {
                            if (ValidImmediateValue(expr)) {
                                AddInstruction(op_loadimm, (int)(double)expr.Value);
                                break;
                            }

                            if (expr.ValueType == sws_DataType.Bool) {
                                AddInstruction(op_loadbool, (bool)expr.Value ? 1 : 0);
                                break;
                            }

                            if (expr.ValueType == sws_DataType.Null) {
                                AddInstruction(op_loadnull);
                                break;
                            }

                            AddInstruction(op_getconst, AddConst(expr.Value, expr.ValueType));

                            break;
                        }
                    case sws_ExpressionType.Variable: {
                            AddVarGetInstruction(expr.Name);
                            break;
                        }
                    case sws_ExpressionType.Table: {
                            if (expr.CanTableBeConst()) {
                                sws_Table swsTable = new sws_Table();
                                Dictionary<sws_Variable, sws_Variable> table = swsTable.Table;

                                for (int i = 0; i < expr.Elements.Count; i++) {
                                    sws_Expression index = expr.ElementIndices[i];
                                    sws_Expression value = expr.Elements[i];
                                    table.Add(new sws_Variable().Constant(index.Value, index.ValueType, -1), new sws_Variable().Constant(value.Value, value.ValueType, -1));
                                }

                                AddInstruction(op_getconst, AddConst(swsTable, sws_DataType.Table));

                                break;
                            }

                            //

                            for (int i = expr.Elements.Count - 1; i >= 0; i--) {
                                ExprToBytecodeRecursive(expr.Elements[i]);
                                ExprToBytecodeRecursive(expr.ElementIndices[i]);
                            }

                            AddInstruction(op_tablenew);

                            if (expr.Elements.Count > 0) {
                                AddInstruction(op_tableset, -expr.Elements.Count);
                            }
                            break;
                        }
                    case sws_ExpressionType.TableGet: {
                            if (expr.Name != null) {
                                AddVarGetInstruction(expr.Name);
                            } else {
                                ExprToBytecodeRecursive(expr.Right);
                            }

                            for (int i = 0; i < expr.Indices.Count; i++) {
                                ExprToBytecodeRecursive(expr.Indices[i]);
                                AddInstruction(op_tableget);
                            }

                            break;
                        }
                    case sws_ExpressionType.LuaCall: {
                            if (!callReturnValues && functionCallParent) {
                                expr.CallValuesReturned = 0;
                                functionCallParent = false;
                            } else if (expr.CallValuesReturned == 0) {
                                expr.CallValuesReturned = 1;
                            }

                            for (int i = expr.Arguments.Count - 1; i >= 0; i--) {
                                ExprToBytecodeRecursive(expr.Arguments[i]);
                            }

                            AddInstruction(op_closure, AddConst("lua " + expr.Name, sws_DataType.String));

                            AddCallInstruction(expr.Arguments.Count, expr.CallValuesReturned);
                            break;
                        }
                    case sws_ExpressionType.ClosureCall: {
                            //Do this bit before the arguments else functionCallParent could be true for an inner function being parsed.
                            if (!callReturnValues && functionCallParent) {
                                //in a case like 'somefunc()()', 'somefunc()' must return a value.
                                if (expr.Parent != null && expr.Parent.Type == sws_ExpressionType.ClosureCall) {
                                    expr.CallValuesReturned = 1;
                                } else {
                                    expr.CallValuesReturned = 0;
                                }
                                functionCallParent = false;
                            } else if (expr.CallValuesReturned == 0) {
                                expr.CallValuesReturned = 1;
                            }

                            //in 'a()()', the last call doesn't return values.
                            if (!callReturnValues && expr.Parent == null) {
                                expr.CallValuesReturned = 0;
                            }

                            if (expr.Left.Name == "_keys") {
                                if (expr.Arguments.Count != 1) {
                                    throw new sws_ParserError(Peek(), $"Expected 1 argument for '_keys', instead got {expr.Arguments.Count}.");
                                }

                                ExprToBytecodeRecursive(expr.Arguments[0]);
                                AddInstruction(op_tablekeys);

                                break;
                            } else if (expr.Left.Name == "_type") {
                                if (expr.Arguments.Count != 1) {
                                    throw new sws_ParserError(Peek(), $"Expected 1 argument for '_type', instead got {expr.Arguments.Count}.");
                                }

                                ExprToBytecodeRecursive(expr.Arguments[0]);
                                AddInstruction(op_type);

                                break;
                            }

                            for (int i = expr.Arguments.Count - 1; i >= 0; i--) {
                                ExprToBytecodeRecursive(expr.Arguments[i]);
                            }

                            ExprToBytecodeRecursive(expr.Left);

                            AddCallInstruction(expr.Arguments.Count, expr.CallValuesReturned);
                            break;
                        }
                    case sws_ExpressionType.SelfCall: {
                            sws_Expression rootCall = expr;

                            for (int i = rootCall.Arguments.Count - 1; i >= 0; i--) {
                                ExprToBytecodeRecursive(rootCall.Arguments[i]);
                            }

                            while (rootCall.Left.Type == sws_ExpressionType.SelfCall) {
                                rootCall = rootCall.Left;

                                for (int i = rootCall.Arguments.Count - 1; i >= 0; i--) {
                                    ExprToBytecodeRecursive(rootCall.Arguments[i]);
                                }
                            }

                            ExprToBytecode(rootCall.Left);

                            SelfCallRecursive();

                            void SelfCallRecursive() {
                                AddInstruction(op_dup);
                                ExprToBytecode(rootCall.Right);
                                AddInstruction(op_tableget);

                                if (!callReturnValues) {
                                    if (functionCallParent) {
                                        rootCall.CallValuesReturned = rootCall.Parent != null && rootCall.Parent.Type == sws_ExpressionType.SelfCall ? 1 : 0;
                                        //special case 'a:b()()', 'a:b()' must return value.
                                        if (expr.Parent != null && expr.Parent.Type == sws_ExpressionType.ClosureCall) {
                                            expr.CallValuesReturned = 1;
                                        }

                                        functionCallParent = false;
                                    }
                                } else if (expr.CallValuesReturned == 0) {
                                    rootCall.CallValuesReturned = 1;
                                }

                                AddCallInstruction(rootCall.Arguments.Count + 1, rootCall.CallValuesReturned);

                                if (rootCall.Parent != null && rootCall.Parent.Type == sws_ExpressionType.SelfCall) {
                                    rootCall = rootCall.Parent;
                                    SelfCallRecursive();
                                }
                            }

                            //throw new sws_Error(Last(), "This type call is currently not implemented.");
                            break;
                        }
                    case sws_ExpressionType.Closure: {
                            if (expr.LuaClosure) {
                                AddVarGetInstruction("lua " + expr.Name);
                            } else {
                                AddVarGetInstruction(expr.Name);
                            }
                            break;
                        }
                }
            }

            void ExprToBytecodeOr(sws_Expression expr) {
                int flag = JMPFLAG + flagModCounter++;

                bool leftSide = (expr.Parent != null) && expr.Parent.Left == expr;

                ExprToBytecodeOrRecursive(expr);

                ResolveJumpFlag(flag, conditionalExpr ? 1 : 0);

                void ExprToBytecodeOrRecursive(sws_Expression e) {
                    if (e.Left.Op == operator_boolor) {
                        ExprToBytecodeOrRecursive(e.Left);
                    } else {
                        ExprToBytecodeRecursive(e.Left);
                    }

                    if (conditionalExpr) {
                        AddInstruction(op_jtrue, flag);
                        conditionalSkipJmpsExceptions.Add(GetPC());
                    } else {
                        AddInstruction(op_jtnp, flag);
                    }

                    ExprToBytecodeRecursive(e.Right);
                }
            }

            void ExprToBytecodeAnd(sws_Expression expr, bool childOfOr) {
                int flag = JMPFLAG + flagModCounter++;

                ExprToBytecodeAndRecursive(expr);

                ResolveJumpFlag(flag, childOfOr ? 1 : 0);

                void ExprToBytecodeAndRecursive(sws_Expression e) {
                    if(e.Left.Op == operator_booland) {
                        ExprToBytecodeAndRecursive(e.Left);
                    } else {
                        ExprToBytecodeRecursive(e.Left);
                    }

                    if (conditionalExpr || childOfOr) {
                        AddJumpFInstruction(flag);
                    } else {
                        AddInstruction(op_jfnp, flag);
                    }

                    ExprToBytecodeRecursive(e.Right);
                }
            }
        }

        /// <summary>
        /// See if next tokens are expression by running expression function and keeping track of starting conditions to revert in case of error. If the expression includes a closure it may even generate bytecode. If the error is one that would only happen if an actual expression was parsed, the errors are printed (the detection for this is very bad, luckly any errors will be caught after the return statement is parsed).
        /// </summary>
        /// <returns></returns>
        private sws_Expression TryGetExpression() {
            int currentTemp = _current;

            try {
                _sws.SuppressParserErrors = true;

                sws_Expression expr = Expression();

                _sws.SuppressParserErrors = false;

                return expr;
            } catch(sws_ParserError e) {
                _sws.SuppressParserErrors = false;
                if (e.Message != "expr_unexpected") {
                    throw new sws_ParserError(e);
                }
                _current = currentTemp;
                return null;
            }
        }

        private sws_Expression Expression() {
            //catch closures first (lua closures are handled in Primary())
            if(Match(keyword_func)) {
                string name = _unnammedFuncCount.ToString();
                
                ParseFunction(true);
                
                return new sws_Expression().Closure(name);
            }
            
            sws_Expression expr = BoolOr();

            return expr;
        }

        private sws_Expression BoolOr() {
            sws_Expression expr = BoolAnd();

            while (Match(operator_boolor)) {
                sws_Expression right = BoolAnd();
                expr = new sws_Expression().Binary(expr, operator_boolor, right);
            }

            return expr;
        }

        private sws_Expression BoolAnd() {
            sws_Expression expr = BitwiseOr();

            while (Match(operator_booland)) {
                sws_Expression right = BitwiseOr();
                expr = new sws_Expression().Binary(expr, operator_booland, right);
            }

            return expr;
        }

        private sws_Expression BitwiseOr() {
            sws_Expression expr = BitwiseXor();

            while (Match(operator_bitor)) {
                sws_Expression right = BitwiseXor();
                expr = new sws_Expression().Binary(expr, operator_bitor, right);
            }

            return expr;
        }

        private sws_Expression BitwiseXor() {
            sws_Expression expr = BitwiseAnd();

            while (Match(operator_bitxor)) {
                sws_Expression right = BitwiseAnd();
                expr = new sws_Expression().Binary(expr, operator_bitxor, right);
            }

            return expr;
        }

        private sws_Expression BitwiseAnd() {
            sws_Expression expr = Equality();

            while (Match(operator_bitand)) {
                sws_Expression right = Equality();
                expr = new sws_Expression().Binary(expr, operator_bitand, right);
            }

            return expr;
        }

        private sws_Expression Equality() {
            sws_Expression expr = Comparison();

            while (Match(new sws_TokenType[] { operator_eq, operator_neq })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Comparison();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private sws_Expression Comparison() {
            sws_Expression expr = BitwiseShifts();

            while (Match(new sws_TokenType[] { operator_gt, operator_gte, operator_lt, operator_lte })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = BitwiseShifts();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private sws_Expression BitwiseShifts() {
            sws_Expression expr = Term();

            while (Match(new sws_TokenType[] { operator_bitshiftleft, operator_bitshiftright })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Term();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private sws_Expression Term() {
            sws_Expression expr = Factor();

            while (Match(new sws_TokenType[] { operator_add, operator_sub, operator_concat })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Factor();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private sws_Expression Factor() {
            sws_Expression expr = Unary();

            while (Match(new sws_TokenType[] { operator_mul, operator_div, operator_floordiv, operator_mod })) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Unary();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private sws_Expression Unary() {
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

        private sws_Expression Exponent() {
            sws_Expression expr = Primary();

            while (Match(operator_pow)) {
                sws_TokenType op = Last().TokenType;
                sws_Expression right = Exponent();
                expr = new sws_Expression().Binary(expr, op, right);
            }

            return expr;
        }

        private sws_Expression Primary() {
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
                        AddLuaClosure(name);
                    }
                }

                TableGet(new List<sws_Expression>());

                //handle function calls
                if (Array.IndexOf(new sws_TokenType[] { identifier, punctuation_brackets_closed, punctuation_parenthesis_closed }, Last().TokenType) != -1 && Match(punctuation_parenthesis_open)) {
                    Call();
                }

                //handle postfix increment and decrement
                if (Array.IndexOf(new sws_TokenType[] { identifier, punctuation_brackets_closed }, Last().TokenType) != -1 && (Match(punctuation_doubleplus) || Match(punctuation_doubleminus))) {
                    IncDec();
                }

                //handle table get
                void TableGet(List<sws_Expression> indices) {
                    if (Peek().TokenType == punctuation_brackets_open || Peek().TokenType == punctuation_period || Peek().TokenType == punctuation_colon) {

                        //Parses 'data["a"]["b"]' and 'data.a.b' syntax for reading table values.
                        bool selfCall = Peek().TokenType == punctuation_colon;
                        Next();

                        if (Last().TokenType == punctuation_brackets_open) {
                            indices.Add(Expression());
                            Consume(punctuation_brackets_closed, "Expected ']' after table index.");
                        } else {
                            if (Peek().TokenType != identifier) {
                                throw new sws_ParserError(Peek(), $"Token type '{Peek().TokenType}' used as table index.");
                            }
                            indices.Add(new sws_Expression().Literal(Peek().Literal, sws_DataType.String));
                            Next();
                        }

                        if (!selfCall) {
                            if (expr.Type != sws_ExpressionType.TableGet) {
                                expr = new sws_Expression().TableGet(expr, indices);
                            }

                            if (Peek().TokenType == punctuation_brackets_open || Peek().TokenType == punctuation_period || Peek().TokenType == punctuation_colon) {
                                TableGet(indices);
                            } else if (Match(punctuation_parenthesis_open)) {
                                Call();
                            }
                        } else {
                            Consume(punctuation_parenthesis_open, $"Expected table value to be called when using ':' syntax.");
                            Call(indices.Last());
                            indices.Remove(indices.Last());
                        }
                    }
                }

                void Call(sws_Expression selfCallName = null) {
                    List<sws_Expression> args = ParseFuncArgs();

                    if (selfCallName != null) {
                        expr = new sws_Expression().SelfCall(expr, selfCallName, args);
                        if (Match(punctuation_colon)) {
                            Consume(identifier, "Expected function name after ':'.");
                            
                            sws_Expression funcName = new sws_Expression().Literal(Last().Literal, sws_DataType.String);
                            
                            Consume(punctuation_parenthesis_open, "Expected '('.");
                            
                            Call(funcName);
                        }
                    } else {
                        expr = new sws_Expression().ClosureCall(expr, args);
                    }

                    //incase a call happens directly after a call (ex. 'test()()')
                    if (Array.IndexOf(new sws_TokenType[] { identifier, punctuation_brackets_closed, punctuation_parenthesis_closed }, Last().TokenType) != -1 && Match(punctuation_parenthesis_open)) {
                        Call();
                    } else {
                        TableGet(new List<sws_Expression>());
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
                                throw new sws_ParserError(Peek(), "Expected function call argument after ','.");
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
                    
                    if (Match(punctuation_brackets_open)) { //[<expr>]=<expr>
                        index = Expression();
                        Consume(punctuation_brackets_closed, "expected ']'.");
                        Consume(operator_assign, "Expected '='.");
                    } else if (Peek2().TokenType == operator_assign) {//<str literal>=<expr>
                        index = new sws_Expression().Literal(Peek());
                        Next(); //skip index
                        Consume(operator_assign, "Expected '='.");
                    } else { //<expr>
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

            throw new sws_ParserError(Peek(), $"Unexpected token '{Peek().TokenType}' in expression.", "expr_unexpected");
        }

        //helper functions for parser start here

        private sws_Token Consume(sws_TokenType tokenType, string errorMessage = "") {
            if (Match(tokenType)) {
                return Last();
            }

            throw new sws_ParserError(Peek(), errorMessage);
        }

        private bool Match(sws_TokenType tokenType) {
            if(AtEnd()) {
                return false;
            }
            if (Peek().TokenType == tokenType) {
                Next();
                return true;
            }
            return false;
        }

        private bool Match(sws_TokenType[] tokenTypes) {
            if (AtEnd()) {
                return false;
            }
            if (Array.IndexOf(tokenTypes, Peek().TokenType) != -1) {
                Next();
                return true;
            }
            return false;
        }

        private sws_Token Last() {
            if (_current == 0) {
                return sws_Token.Error(_lastToken);
            }
            _lastToken = _tokens[_current - 1];
            return _lastToken;
        }

        private sws_Token Next() {
            if(AtEnd()) {
                return sws_Token.Error(_lastToken);
            }
            _lastToken = _tokens[_current++];
            return _lastToken;
        }

        private sws_Token Peek() {
            if (AtEnd()) {
                return sws_Token.Error(_lastToken);
            }
            _lastToken = _tokens[_current];
            return _lastToken;
        }

        private sws_Token Peek2() {
            if (_current + 1 >= _tokens.Count - 1) {
                return sws_Token.Error(_lastToken);
            }
            _lastToken = _tokens[_current + 1];
            return _lastToken;
        }

        private bool AtEnd() {
            return _current >= _tokens.Count - 1;
        }

        /// <summary>
        /// Used as part of optimization for seeing if instructions like op_loadimm or op_addimm can be used. Checks if expression given is value which can be stored in 18 bit data section of instruction.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ValidImmediateValue(sws_Expression value) {
            return  value.Type == sws_ExpressionType.Literal &&                         //check value is literal
                    value.ValueType == sws_DataType.Double &&                           //check literal is number
                    (double)value.Value % 1 == 0 &&                                     //check number is integer
                    (double)value.Value >= -131071 && (double)value.Value <= 131071;    //check if range is between -2^17+1 and 2^17-1
        }

        //Originally part of sws_Prg, which was combined with this file

        private void PushFrame(string name, bool declareAtStart) {
            if (!_frames.ContainsKey(name)) {
                _frames.Add(name, new sws_Frame(name));
            }

            if (name != string.Empty) {
                _frame.Closures.Add(name);
                AddVariable(name, sws_VariableType.Global);

                AddInstruction(op_closure, AddConst(name, sws_DataType.String));
                AddVarSetInstruction(name);

                if (declareAtStart) {
                    //move instructions to start of program
                    for (int i = 0; i < 2; i++) {
                        sws_Op instruction = LastInstruction();
                        RemoveLastInstruction();
                        AddInstructionToStart(instruction.Opcode, instruction.Data);
                    }
                }
            }

            _frame = _frames[name];

            _frameStack.Push(_frame);
        }

        private void PushNoName() {
            string name = _unnammedFuncCount.ToString();

            _frames.Add(name, new sws_Frame(name));

            _frame.Closures.Add(name);
            AddVariable(name, sws_VariableType.Global);

            AddInstruction(op_closure, AddConst(name, sws_DataType.String));
            AddVarSetInstruction(name);

            _frame = _frames[name];

            _frameStack.Push(_frame);

            _unnammedFuncCount++;
        }

        private void AddLuaClosure(string name) {
            name = "lua " + name;

            _frame.Closures.Add(name);
            AddVariable(name, sws_VariableType.Global);

            AddInstruction(op_closure, AddConst(name, sws_DataType.String));
            AddVarSetInstruction(name);

            //move instructions to start of program

            for (int i = 0; i < 2; i++) {
                sws_Op instruction = LastInstruction();
                RemoveLastInstruction();
                AddInstructionToStart(instruction.Opcode, instruction.Data);
            }
        }

        private void PopFrame() {
            if (_frameStack.Count == 1) {
                throw new sws_ParserError(_lastToken, "Attempt to return from main.");
            }

            _frameStack.Pop();

            _frame = _frameStack.Peek();
        }

        private int AddInstruction(sws_Opcode opcode, int data = int.MinValue) {
            //Optimization: if opcode is op_add, check if one of two previous instructions is op_loadimm n, in which case it can be replaced with an op_addimm instruction.
            if (opcode == op_add) {
                sws_Op last = LastInstruction();
                if (last != null && last.Opcode == op_loadimm) {
                    int n = last.Data;
                    RemoveLastInstruction();
                    _frame.Program.Add(new sws_Op(op_addimm, n, Last().NLine));
                    return GetPC();
                }

                sws_Op secondToLast = SecondToLastInstruction();
                if (secondToLast != null && secondToLast.Opcode == op_loadimm) {
                    int n = secondToLast.Data;
                    RemoveSecondToLastInstruction();
                    _frame.Program.Add(new sws_Op(op_addimm, n, Last().NLine));
                    return GetPC();
                }
            }
            //Optimization: op_concat can work on multiple values in a similar way to op_tableset, by specifying the number to concat in the operand. Check if previous instruction is op_concat, and increment it's operand's value by 1 instead of adding this one.
            else if (opcode == op_concat) {
                sws_Op last = LastInstruction();
                if (last != null && last.Opcode == op_concat) {
                    last.Data++; //technically this has an edge case if you somehow go over the 2^17-1 limit for the operand, but that's never gonna happen anyway, right?.
                    return GetPC();
                }
            }

            _frame.Program.Add(new sws_Op(opcode, data, Last().NLine));
            return GetPC();
        }

        private void AddCallInstruction(int argCount, int expectedReturnCount) {
            int data = (argCount & 0xFF) + ((expectedReturnCount & 0xFF) << 8);

            // NOTE: only takes of 16 bits of 17 available.
            if (argCount > 255) {
                throw new sws_ParserError(_lastToken, "Number of arguments for function call exceeded 255.");
            }

            if (expectedReturnCount > 255) {
                throw new sws_ParserError(_lastToken, "Number of values expected to be returned from function exceeded 255.");
            }

            AddInstruction(op_call, data);
        }

        private void InsertInstruction(int pc, sws_Opcode opcode, int data = int.MinValue) {
            _frame.Program.Insert(pc, new sws_Op(opcode, data, Last().NLine));
        }

        private void AddInstructionToStart(sws_Opcode opcode, int data = int.MinValue) {
            InsertInstruction(0, opcode, data);
        }

        public sws_Op LastInstruction() {
            return _frame.Program.Last();
        }

        private sws_Op SecondToLastInstruction() {
            if (_frame.Program.Count > 1) {
                return _frame.Program[_frame.Program.Count - 2];
            }
            return null;
        }

        private void RemoveLastInstruction() {
            if (_frame.Program.Any()) {
                _frame.Program.RemoveAt(_frame.Program.Count - 1);
            }
        }

        private void RemoveSecondToLastInstruction() {
            if (_frame.Program.Count > 1) {
                _frame.Program.RemoveAt(_frame.Program.Count - 2);
            }
        }

        private void SetLocalScopeStart() {
            for (int i = 0; i < _frame.Locals.Count; i++) {
                sws_Variable local = _frame.Locals[i];
                if (local.Depth == _depth && local.ScopeStart == -1) {
                    local.ScopeStart = _frame.Program.Count;
                }
            }
        }

        private void SetLocalScopeEnd() {
            for (int i = 0; i < _frame.Locals.Count; i++) {
                sws_Variable local = _frame.Locals[i];
                if (local.Depth == _depth && local.ScopeEnd == -1) {
                    local.ScopeEnd = _frame.Program.Count;
                }
            }
        }

        /// <summary>
        /// Adds a variable as either a local, upvalue, or global so it's index can later be retrieved.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="forceType">If sws_VariableType.Constant is entered a variable type will not be forced.</param>
        private void AddVariable(string name, sws_VariableType forceType = sws_VariableType.Constant) {
            if (forceType == sws_VariableType.Global) {
                sws_Variable variable = GetGlobal(name);
                if (variable == null) {
                    _globals.Add(new sws_Variable().Global(name, _globals.Count));
                }
                return;
            }

            //force variable to be local
            if (forceType == sws_VariableType.Local) {
                sws_Variable local = GetLocal(name);

                if (local == null) {
                    _frame.Locals.Add(new sws_Variable().Local(name, _frame.Locals.Count, -1, _depth));
                    return;
                }

                throw new sws_ParserError(_lastToken, $"Local variable '{name}' already exists in scope.");
            } else {
                //if variable being local is not forced, still check and return if it exists as a local. If it doesn't continue.
                if (GetLocal(name) != null) {
                    return;
                }
            }

            //search for locals in functions under this one in depth which match in name. If a match is found the variable is added as an upvalue.
            sws_Variable upvalue = GetUpvalue(name);
            if (upvalue == null) {
                sws_Frame[] framesArray = _frameStack.ToArray(); //has to be reversed because making a copy of the stack reverses it.
                Stack<sws_Frame> frames = new Stack<sws_Frame>(framesArray.Reverse());
                frames.Pop();
                while (frames.Count > 0) {
                    sws_Frame frame = frames.Pop();
                    int localIndex = frame.GetLocal(name);
                    if (localIndex != -1) {
                        _frame.Upvalues.Add(new sws_Variable().Upvalue(name, _frame.Upvalues.Count, frame.Name, localIndex));
                        return;
                    }
                }
            } else {
                //if upvalue was found return, otherwise global variable will be added.
                return;
            }

            //if searching for upvalues fails, add the variable as a global.
            sws_Variable global = GetGlobal(name);
            if (global == null) {
                _globals.Add(new sws_Variable().Global(name, _globals.Count));
            }
        }

        private int AddConst(object value, sws_DataType dataType) {
            int index = GetConst(value, dataType);

            if (index == -1) {
                index = _constants.Count;
                _constants.Add(new sws_Variable().Constant(value, dataType, index));
            }

            return index;
        }

        /// <summary>
        /// Adds instruction to get variable with given name.
        /// </summary>
        /// <param name="name"></param>
        private void AddVarGetInstruction(string name) {
            sws_Variable variable = GetVariable(name);

            //if variable doesn't exist assume it's a global
            if (variable == null) {
                variable = new sws_Variable().Global(name, _globals.Count);
                _globals.Add(variable);
                AddInstruction(op_getglobal, variable.Index);
                return;
            }

            if (variable.VariableType == sws_VariableType.Local) {
                AddInstruction(op_getlocal, variable.Index);
                return;
            }

            if (variable.VariableType == sws_VariableType.Upvalue) {
                AddInstruction(op_getupval, variable.Index);
                return;
            }

            if (variable.VariableType == sws_VariableType.Global) {
                AddInstruction(op_getglobal, variable.Index);
                return;
            }
        }

        /// <summary>
        /// Adds instruction to set variable with given name.
        /// </summary>
        /// <param name="name"></param>
        private void AddVarSetInstruction(string name) {
            sws_Variable variable = GetVariable(name);

            //if variable doesn't exist assume it's a global
            if (variable == null) {
                variable = new sws_Variable().Global(name, _globals.Count);
                _globals.Add(variable);
                AddInstruction(op_setglobal, variable.Index);
                return;
            }

            if (variable.VariableType == sws_VariableType.Local) {
                AddInstruction(op_setlocal, variable.Index);
                return;
            }

            if (variable.VariableType == sws_VariableType.Upvalue) {
                AddInstruction(op_setupval, variable.Index);
                return;
            }

            if (variable.VariableType == sws_VariableType.Global) {
                AddInstruction(op_setglobal, variable.Index);
                return;
            }
        }

        /// <summary>
        /// Get variable with given name. Priority is given to locals, than upvalues, than globals
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private sws_Variable GetVariable(string name) {
            sws_Variable variable = GetLocal(name);
            if (variable != null) {
                return variable;
            }

            variable = GetUpvalue(name);
            if (variable != null) {
                return variable;
            }

            variable = GetGlobal(name);
            if (variable != null) {
                return variable;
            }

            return null;
        }

        private int GetConst(object value, sws_DataType dataType) {
            for (int i = 0; i < _constants.Count; i++) {
                sws_Variable constant = _constants[i];

                if (constant.ValueType != dataType) {
                    continue;
                }

                switch (constant.ValueType) {
                    case sws_DataType.Null:
                        return i;
                    case sws_DataType.Bool:
                        if ((bool)value == (bool)constant.Value) {
                            return i;
                        }
                        break;
                    case sws_DataType.Double:
                        if ((double)value == (double)constant.Value) {
                            return i;
                        }
                        break;
                    case sws_DataType.String:
                        if (value.ToString() == constant.Value.ToString()) {
                            return i;
                        }
                        break;
                }
            }
            return -1;
        }

        private sws_Variable GetLocal(string name) {
            for (int i = 0; i < _frame.Locals.Count; i++) {
                sws_Variable local = _frame.Locals[i];
                //ScopeEnd being -1 means the variable is inside the scope of where the parser is currently.
                if (local.ScopeEnd == -1 && name == local.Name) {
                    return local;
                }
            }

            return null;
        }

        private sws_Variable GetUpvalue(string name) {
            for (int i = 0; i < _frame.Upvalues.Count; i++) {
                if (name == _frame.Upvalues[i].Name) {
                    return _frame.Upvalues[i];
                }
            }

            //if it doesn't exist, search for upvalue and add it if found.
            sws_Frame[] framesArray = _frameStack.ToArray(); //has to be reversed because making a copy of the stack reverses it.
            Stack<sws_Frame> frames = new Stack<sws_Frame>(framesArray.Reverse());
            frames.Pop();
            while (frames.Count > 0) {
                sws_Frame frame = frames.Pop();
                int localIndex = frame.GetLocal(name);
                if (localIndex != -1) {
                    _frame.Upvalues.Add(new sws_Variable().Upvalue(name, _frame.Upvalues.Count, frame.Name, localIndex));
                    return _frame.Upvalues.Last();
                }
            }

            return null;
        }

        private sws_Variable GetGlobal(string name) {
            for (int i = 0; i < _globals.Count; i++) {
                if (name == _globals[i].Name) {
                    return _globals[i];
                }
            }

            return null;
        }

        private int GetPC() {
            return _frame.Program.Count - 1;
        }

        private void ResolveJumpFlag(int flag, int offset = 0) {
            for (int i = 0; i < _frame.Program.Count; i++) {
                sws_Op instruction = _frame.Program[i];

                if (instruction.Data == flag) {
                    instruction.Data = _frame.Program.Count - i - 1 + offset;
                }
            }
        }

        private void ResolveJumpPcCustomDest(int pc, int destPC) {
            _frame.Program[pc].Data = destPC - pc;
        }

        private void ResolveJumpPC(int pc) {
            _frame.Program[pc].Data = _frame.Program.Count - pc - 1;
        }

        private void ResolveJumpPCs(List<int> pcs, int offset = 0) {
            for (int i = 0; i < pcs.Count; i++) {
                ResolveJumpPC(pcs[i] + offset);
            }
        }

        /// <summary>
        /// Optimizes when instructions such as op_eq and op_jfalse follow eachother into a single instruction.
        /// </summary>
        /// <param name="flag"></param>
        private void AddJumpFInstruction(int flag) {
            sws_Opcode last = LastInstruction().Opcode;
            switch (last) {
                case op_eq:
                    RemoveLastInstruction();
                    AddInstruction(op_jneq, flag);
                    break;
                case op_neq:
                    RemoveLastInstruction();
                    AddInstruction(op_jeq, flag);
                    break;
                case op_lt:
                    RemoveLastInstruction();
                    AddInstruction(op_jgte, flag);
                    break;
                case op_lte:
                    RemoveLastInstruction();
                    AddInstruction(op_jgt, flag);
                    break;
                default:
                    AddInstruction(op_jfalse, flag);
                    break;
            }
        }

        private bool JumpFCanSimplify() {
            return Array.IndexOf(new sws_Opcode[] { op_eq, op_neq, op_lt, op_lte }, LastInstruction().Opcode) != -1;
        }

        private void ResolveContinueBreakLoop(List<int> instructionIndices, int continuePC, int breakPC) {
            for (int i = 0; i < instructionIndices.Count; i++) {
                int pc = instructionIndices[i];
                sws_Op instruction = _frame.Program[pc];

                if (instruction.Data == JMP_CONTINUE) {
                    instruction.Data = continuePC - pc;
                } else if (instruction.Data == JMP_BREAK) {
                    instruction.Data = breakPC - pc;
                }
            }
        }

        private void ResolveIfEnd(List<int> instructionIndices, int endPC) {
            for (int i = 0; i < instructionIndices.Count; i++) {
                int pc = instructionIndices[i];
                sws_Op instruction = _frame.Program[pc];

                if (instruction.Data == JMP_IFEND) {
                    instruction.Data = endPC - pc;
                }
            }
        }
    }
}
