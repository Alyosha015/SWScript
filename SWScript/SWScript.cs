using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SWLuaSim;
using SWS.Compiler;
using SWS.Properties;

namespace SWS {
    public enum MsgType {
        ErrTokenize,
        ErrCompile,
        ErrRuntime,
        Warning,
        Message,
    }

    public class SWScript {
        /// <summary>
        /// Language Version.
        /// </summary>
        public string SWSVersion = @"1.1.0";

        public bool CompileSuccess;
        public sws_Program Program;

        private string _source;
        private string[] _sourceLines;
        private List<sws_Token> _tokens;

        private sws_TokenizerFirstPass _tokenizerFirstPass;
        private sws_TokenizerSecondPass _tokenizerSecondPass;
        private sws_Parser _parser;

        internal bool SuppressTokenizerErrors;
        public int TokenizerErrorCount;

        internal bool SuppressParserWarnings;
        public int ParserWarningCount;

        internal bool SuppressParserErrors;
        public int ParserErrorCount;

        internal bool SuppressRuntimeErrors;
        public int RuntimeErrorCount;

        /// <summary>
        /// Runs on sws 'print' command. 'Console.Write' used by default.
        /// </summary>
        public Action<string> Print;

        private void DefaultPrint(string s) {
            Console.Write(s);
        }

        /// <summary>
        /// Runs on sws 'println' command. 'Console.WriteLine' used by default.
        /// </summary>
        public Action<string> PrintLn;

        private void DefaultPrintLn(string s) {
            Console.WriteLine(s);
        }

        /// <summary>
        /// Runs on error/warning/messaage from SWScript compiler/interpreter. Parameters: (message type, line #, col #, source line, message)
        /// </summary>
        public Action<MsgType, int, int, string, string> Message;

        private void DefaultMessage(MsgType msgType, int nLine, int nColumn, string line, string message) {
            switch(msgType) {
                case MsgType.ErrTokenize: {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[TOKENIZER ERROR] @ line:{nLine} {message}");

                        if (nLine != -1) {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine(_sourceLines[nLine - 1]);
                            Console.WriteLine(new string(' ', nColumn - 1) + '^');
                        }

                        Console.ResetColor();
                        break;
                    }
                case MsgType.ErrCompile: {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[COMPILER ERROR] @ line:{nLine} {message}");

                        if (nLine != -1) {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine(_sourceLines[nLine - 1]);
                            Console.WriteLine(new string(' ', nColumn - 1) + '^');
                        }

                        Console.ResetColor();
                        break;
                    }
                case MsgType.ErrRuntime: {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[RUNTIME ERROR] @ line:{nLine} {message}");

                        if (nLine != -1) {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine(_sourceLines[nLine - 1]);
                            Console.WriteLine(new string(' ', nColumn - 1) + '^');
                        }

                        Console.ResetColor();
                        break;
                    }
                case MsgType.Warning: {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[COMPILER WARNING] @ line:{nLine} {message}");

                        if (nLine != -1) {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine(_sourceLines[nLine - 1]);
                            Console.WriteLine(new string(' ', nColumn - 1) + '^');
                        }

                        Console.ResetColor();
                        break;
                    }
                case MsgType.Message: {
                        Console.WriteLine(message);
                        break;
                    }
            }
        }

        /// <summary>
        /// Any messages should call here, this bit of code always runs and is responsible for counting how many errors/warnings/etc there are.
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="nLine"></param>
        /// <param name="nColumn"></param>
        /// <param name="line"></param>
        /// <param name="message"></param>
        internal void MessageAction(MsgType msgType, int nLine, int nColumn, string line, string message) {
            switch(msgType) {
                case MsgType.ErrTokenize: {
                        if (SuppressTokenizerErrors) return;
                        TokenizerErrorCount++;
                        break;
                    }
                case MsgType.ErrCompile: {
                        if(SuppressParserErrors) return;
                        ParserErrorCount++;
                        break;
                    }
                case MsgType.ErrRuntime: {
                        if(SuppressRuntimeErrors) return;
                        RuntimeErrorCount++;
                        break;
                    }
                case MsgType.Warning: {
                        if (SuppressParserWarnings) return;
                        ParserWarningCount++;
                        break;
                    }
                case MsgType.Message: {
                        break;
                    }
            }

            Message(msgType, nLine, nColumn, line, message);
        }

        internal void Msg(string message) {
            MessageAction(MsgType.Message, -1, -1, string.Empty, message);
        }

        private void ResetErrors() {
            SuppressTokenizerErrors = false;
            TokenizerErrorCount = 0;

            SuppressParserWarnings = false;
            ParserWarningCount = 0;

            SuppressParserErrors = false;
            ParserErrorCount = 0;

            SuppressRuntimeErrors = false;
            RuntimeErrorCount = 0;
        }

        public SWScript() {
            Print = DefaultPrint;
            PrintLn = DefaultPrintLn;
            Message = DefaultMessage;

            _tokenizerFirstPass = new sws_TokenizerFirstPass();
            _tokenizerSecondPass = new sws_TokenizerSecondPass();
            _parser = new sws_Parser();
        }

        public sws_Program Compile(string source) {
            _source = source;
            _sourceLines = _source.Replace("\n", "\n").Split('\n');

            CompileSuccess = false;

            ResetErrors();

            List<sws_Token> firstPassTokens = _tokenizerFirstPass.Tokenize(this, _source, _sourceLines);

            if (TokenizerErrorCount > 0) {
                Message(MsgType.ErrCompile, 1, 1, "", "Unable to continue compilation process due to errors tokenizing file.");
                return null;
            }

            _tokens = _tokenizerSecondPass.Tokenize(firstPassTokens);
            Program = _parser.Parse(this, _source, _sourceLines, _tokens);

            CompileSuccess = Program != null;

            return Program;
        }

        public void Run(bool useColor, bool fast, bool includeDebugData, bool instructionStats, bool callStats, int w, int h, string vm) {
            if (!CompileSuccess) {
                return;
            }

            AnsiColor.Enable = useColor;

            if (vm == string.Empty) {
                vm = fast ? Resources.FastVM : Resources.DebugVM;
            }

            string[] programData = SplitProgramData(GeneratePackagedProgram(Program, includeDebugData, instructionStats, callStats));

            Dictionary<string, string> propertyTexts = new Dictionary<string, string>(Program.PropertyTexts);
            Dictionary<string, double> propertyNumbers = new Dictionary<string, double>(Program.PropertyNumbers);

            if(!propertyNumbers.ContainsKey("SWS")) {
                propertyNumbers.Add("SWS", programData.Length);
            }
            for (int i = 0; i < programData.Length; i++) {
                if (!propertyNumbers.ContainsKey("SWS" + (i + 1))) {
                    propertyTexts.Add("SWS" + (i + 1), programData[i]);
                }
            }

            StormworksLuaSim luaSim = new StormworksLuaSim(Print, PrintLn, vm, Program.StackFrames.ContainsKey("onTick") || Program.StackFrames.ContainsKey("onDraw"), propertyTexts, propertyNumbers, Program.PropertyBools, Math.Max(w * 32, 32), Math.Max(h * 32, 32));

            AnsiColor.Enable = true;
        }

        /// <summary>
        /// Generates token printout.
        /// </summary>
        /// <param name="useColor"></param>
        public string TokenPrintout(bool useColor) {
            if (!CompileSuccess) {
                return "";
            }

            AnsiColor.Enable = useColor;
            StringBuilder output = new StringBuilder();

            output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}     #   Type                                  Ln / Col: Literal");

            List<sws_Token> tokens = Program.Tokens;
            for (int i = 0; i < tokens.Count; i++) {
                sws_Token token = tokens[i];

                string line = $"{new string(' ', 6 - i.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{i}   {AnsiColor.Color(FG.BrightWhite)}{token.TokenType}{new string(' ', Math.Max(0, 35 - token.TokenType.ToString().Length))}{AnsiColor.ResetWriteColor}{new string(' ', 5 - token.NLine.ToString().Length)}{token.NLine} / {new string(' ', 3 - token.NColumn.ToString().Length)}{token.NColumn}: {AnsiColor.Color(FG.BrightWhite)}{token.Literal}";

                output.AppendLine(line);
            }

            output.Append(AnsiColor.ResetWriteColor);

            AnsiColor.Enable = true;
            return output.ToString();
        }

        /// <summary>
        /// Generates bytecode printout.
        /// </summary>
        /// <param name="useColor"></param>
        public string BytecodePrintout(bool useColor) {
            if (!CompileSuccess) {
                return "";
            }

            AnsiColor.Enable = useColor;
            StringBuilder output = new StringBuilder();

            sws_Frame[] frames = Program.StackFrames.Values.ToArray();

            for (int i = 0; i < frames.Length; i++) {
                if (i != 0) {
                    output.AppendLine($"{AnsiColor.ResetWriteColor}* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *");
                }

                sws_Frame frame = frames[i];

                string funcName = frame.Name;

                output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}func {AnsiColor.Color(FG.Yellow)}{funcName}{AnsiColor.Color(FG.BrightWhite)}() {'{'}");

                List<sws_Op> bytecode = frame.Program;

                for (int j = 0; j < bytecode.Count; j++) {
                    sws_Op op = bytecode[j];
                    
                    string line = $"{new string(' ', 6 - j.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{j}  {AnsiColor.ResetWriteColor}{op.Opcode} {AnsiColor.Color(FG.BrightWhite)}{(op.Data == int.MinValue ? string.Empty : op.Data.ToString())}";
                    int lineLength = line.Length - (AnsiColor.Color(FG.Cyan) + AnsiColor.ResetWriteColor + AnsiColor.Color(FG.BrightWhite)).Length;

                    string lineComment = $"{new string(' ', Math.Max(30 - lineLength, 1))}{AnsiColor.Color(FG.Gray)};";

                    if (op.IsJumpOp()) {
                        line += $"{lineComment}Jump To:{j + op.Data + 1}";
                    } else if (op.IsDataOp()) {
                        line += $"{lineComment}";

                        if (op.Opcode == sws_Opcode.op_getconst) {
                            sws_Variable constant = Program.Constants[op.Data];

                            line += $"{(constant.ValueType == sws_DataType.Table ? constant.TableToStr() : constant.Value)} ({constant.ValueType})";
                        } else if (op.Opcode == sws_Opcode.op_getlocal || op.Opcode == sws_Opcode.op_setlocal) {
                            line += frame.Locals[op.Data].Name;
                        } else if (op.Opcode == sws_Opcode.op_getupval || op.Opcode == sws_Opcode.op_setupval) {
                            line += frame.Upvalues[op.Data].Name;
                        } else if (op.Opcode == sws_Opcode.op_getglobal || op.Opcode == sws_Opcode.op_setglobal) {
                            line += Program.Globals[op.Data].Name;
                        }
                    } else if (op.Opcode == sws_Opcode.op_closure) {
                        line += $"{lineComment}{Program.Constants[op.Data].Value}";
                    } else if (op.Opcode == sws_Opcode.op_call) {
                        int data = op.Data;
                        line += $"{lineComment}Args:{data & 0xFF} Returns:{(data & 0xFF00) >> 8}";
                    }

                    output.AppendLine(line);
                }

                output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}{'}'}\n");

                //frame locals
                List<sws_Variable> locals = frame.Locals;

                output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}Locals ({AnsiColor.Color(FG.Yellow)}{locals.Count}{AnsiColor.Color(FG.BrightWhite)})");

                for (int j = 0; j < locals.Count; j++) {
                    sws_Variable local = locals[j];

                    output.AppendLine($"{new string(' ', 6 - j.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{j}{AnsiColor.ResetWriteColor}  {local.Name}  {AnsiColor.Color(FG.Gray)}{local.ScopeStart}, {local.ScopeEnd}, {local.Depth}");
                }

                output.AppendLine();

                //frame upvalues
                List<sws_Variable> upvalues = frame.Upvalues;

                output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}Upvalues ({AnsiColor.Color(FG.Yellow)}{upvalues.Count}{AnsiColor.Color(FG.BrightWhite)})");

                for (int j = 0; j < upvalues.Count; j++) {
                    sws_Variable upvalue = upvalues[j];

                    output.AppendLine($"{new string(' ', 6 - j.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{j}{AnsiColor.ResetWriteColor}  {upvalue.Name}  {AnsiColor.Color(FG.Gray)}{upvalue.StackFrameReference}, {upvalue.LocalIndexReference}");
                }

                output.AppendLine();

                //frame closures
                List<string> closures = frame.Closures;

                output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}Closures ({AnsiColor.Color(FG.Yellow)}{closures.Count}{AnsiColor.Color(FG.BrightWhite)})");

                for (int j = 0; j < closures.Count; j++) {
                    output.AppendLine($"{new string(' ', 6 - j.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{j}{AnsiColor.ResetWriteColor}  {closures[j]}");
                }

                output.AppendLine();
            }

            //globals
            output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}**** Globals ({AnsiColor.Color(FG.Yellow)}{Program.Globals.Count}{AnsiColor.Color(FG.BrightWhite)}) ****");
            
            for (int i = 0; i < Program.Globals.Count; i++) {
                sws_Variable global = Program.Globals[i];
                output.AppendLine($"{new string(' ', 6 - i.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{i}{AnsiColor.ResetWriteColor}  {global.Name}");
            }

            output.AppendLine();

            //constants
            output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}**** Constants ({AnsiColor.Color(FG.Yellow)}{Program.Constants.Count}{AnsiColor.Color(FG.BrightWhite)}) ****");

            for (int i = 0; i < Program.Constants.Count; i++) {
                sws_Variable constant = Program.Constants[i];
                output.AppendLine($"{new string(' ', 6 - i.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{i}{AnsiColor.ResetWriteColor}  {(constant.ValueType == sws_DataType.Table ? constant.TableToStr() : constant.Value)}  {AnsiColor.Color(FG.Gray)}({constant.ValueType})");
            }

            output.AppendLine();

            //property sets
            //text
            output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}**** Property Texts ({AnsiColor.Color(FG.Yellow)}{Program.PropertyTexts.Count}{AnsiColor.Color(FG.BrightWhite)}) ****");

            int propertyTextCounter = 0;
            foreach (var pair in Program.PropertyTexts) {
                propertyTextCounter++;
                output.AppendLine($"{new string(' ', 6 - propertyTextCounter.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{propertyTextCounter}{AnsiColor.ResetWriteColor}  {pair.Key}: {pair.Value}");
            }

            output.AppendLine();

            //numbers
            output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}**** Property Numbers ({AnsiColor.Color(FG.Yellow)}{Program.PropertyNumbers.Count}{AnsiColor.Color(FG.BrightWhite)}) ****");

            int propertyNumberCounter = 0;
            foreach (var pair in Program.PropertyNumbers) {
                propertyNumberCounter++;
                output.AppendLine($"{new string(' ', 6 - propertyNumberCounter.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{propertyNumberCounter}{AnsiColor.ResetWriteColor}  {pair.Key}: {pair.Value}");
            }

            output.AppendLine();

            //bools
            output.AppendLine($"{AnsiColor.Color(FG.BrightWhite)}**** Property Bools ({AnsiColor.Color(FG.Yellow)}{Program.PropertyNumbers.Count}{AnsiColor.Color(FG.BrightWhite)}) ****");

            int propertyBoolCounter = 0;
            foreach (var pair in Program.PropertyBools) {
                propertyBoolCounter++;
                output.AppendLine($"{new string(' ', 6 - propertyBoolCounter.ToString().Length)}{AnsiColor.Color(FG.Cyan)}{propertyBoolCounter}{AnsiColor.ResetWriteColor}  {pair.Key}: {pair.Value}");
            }

            output.Append(AnsiColor.ResetWriteColor);

            AnsiColor.Enable = true;
            return output.ToString();
        }

        /// <summary>
        /// Generates stormworks vehicle file containing vm and program.
        /// </summary>
        /// <returns></returns>
        public string ExportToSW(string nonDefaultVm = "") {
            if (!CompileSuccess) {
                return "";
            }

            StringBuilder output = new StringBuilder();

            string[] programData = SplitProgramData(GeneratePackagedProgram(Program, false, false, false));

            //default vm
            string sws_LuaVM = nonDefaultVm == string.Empty ? Resources.MinimizedVM : nonDefaultVm;

            sws_LuaVM = FormatForXml(sws_LuaVM);

            string VEHICLE_FILE_START = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><vehicle data_version=\"3\" bodies_id=\"2\"><authors/><bodies><body unique_id=\"2\"><components><c d=\"microprocessor\"><o r=\"1,0,0,0,1,0,0,0,1\" sc=\"6\"><microprocessor_definition name=\"SWScript\" width=\"1\" length=\"1\" id_counter=\"4\" id_counter_node=\"0\"><nodes/><group><data><inputs/><outputs/></data><components><c type=\"56\"><object id=\"1\" script='{sws_LuaVM}'><pos x=\"-0.5\" y=\"0.25\"/></object></c><c type=\"34\"><object id=\"2\" n=\"SWS\"><pos x=\"-0.5\" y=\"-0.5\"/><v text=\"{programData.Length}\" value=\"{programData.Length}\"/></object></c>";
            string VEHICLE_FILE_END = "</components><components_bridge/><groups/></group></microprocessor_definition><vp y=\"1\"/><logic_slots/></o></c></components></body></bodies><logic_node_links/></vehicle>";

            output.Append(VEHICLE_FILE_START);

            for (int i = 0; i < programData.Length; i++) {
                output.Append($"<c type=\"58\"><object id=\"4\" n=\"SWS{i + 1}\" v=\"{programData[i]}\"><pos x=\"-0.5\" y=\"-1.25\"/></object></c>");
            }

            foreach (var item in Program.PropertyTexts) {
                output.Append($"<c type=\"58\"><object id=\"4\" n=\"{FormatForXml(item.Key)}\" v=\"{FormatForXml(item.Value)}\"><pos x=\"-0.5\" y=\"-2\"/></object></c>");
            }

            foreach (var item in Program.PropertyNumbers) {
                output.Append($"<c type=\"34\"><object id=\"2\" n=\"{FormatForXml(item.Key)}\"><pos x=\"-0.5\" y=\"-2\"/><v text=\"{FormatForXml(item.Value.ToString())}\" value=\"{FormatForXml(item.Value.ToString())}\"/></object></c>");
            }

            foreach (var item in Program.PropertyBools) {
                output.Append($"<c type=\"33\"><object id=\"3\" n=\"{FormatForXml(item.Key)}\" on=\"True\" off=\"False\" v=\"{FormatForXml(item.Value.ToString()).ToLower()}\"><pos x=\"-0.5\" y=\"-2\"/></object></c>");
            }

            output.Append(VEHICLE_FILE_END);

            return output.ToString();
        }

        /// <summary>
        /// Generates property set commands needed for lifeboat api.
        /// </summary>
        /// <param name="includeDebugData"></param>
        public string ExportToLB(bool includeDebugData, bool instructionStats, bool callStats) {
            if (!CompileSuccess) {
                return "";
            }

            StringBuilder output = new StringBuilder();

            string[] programData = SplitProgramData(GeneratePackagedProgram(Program, includeDebugData, instructionStats, callStats));

            output.AppendLine($"simulator:setProperty(\"SWS\", {programData.Length})");

            for (int i = 0; i < programData.Length; i++) {
                output.AppendLine($"simulator:setProperty(\"SWS{i + 1}\", \"{programData[i]}\")");

            }

            foreach (var item in Program.PropertyTexts) {
                output.AppendLine($"simulator:setProperty(\"{item.Key}\", \"{item.Value}\")");
            }

            foreach (var item in Program.PropertyNumbers) {
                output.AppendLine($"simulator:setProperty(\"{item.Key}\", \"{item.Value}\")");
            }

            foreach (var item in Program.PropertyBools) {
                output.AppendLine($"simulator:setProperty(\"{item.Key}\", \"{item.Value}\")");
            }

            string outputStr = output.ToString();
            return outputStr.Remove(outputStr.Length - 1);
        }

        /// <summary>
        /// Converts sws_Program to hex string format for vm.
        /// </summary>
        /// <param name="program"></param>
        /// <param name="includeDebugData">Adds data used by vm for reporing error messages. Don't use if exporting for stormworks lua vm.</param>
        /// <returns></returns>
        public static string GeneratePackagedProgram(sws_Program program, bool includeDebugData, bool instructionStats, bool callStats) {
            if (program == null) {
                return "";
            }

            StringBuilder bin = new StringBuilder();

            char M1 = (char)1; //used as marker between values.

            sws_Frame[] frames = program.StackFrames.Values.ToArray();

            Append(frames.Length);

            //export stack frames
            for (int i = 0; i < frames.Length; i++) {
                sws_Frame frame = frames[i];
                Append(frame.Name);

                //bytecode
                Append(frame.Program.Count);
                for (int j = 0; j < frame.Program.Count; j++) {
                    Append(frame.Program[j].ExportToInt());
                }

                //local data
                Append(frame.Locals.Count);

                //upvalue data
                Append(frame.Upvalues.Count);
                for (int j = 0; j < frame.Upvalues.Count; j++) {
                    Append(frame.Upvalues[j].StackFrameReference);
                    Append(frame.Upvalues[j].LocalIndexReference);
                }
            }

            //globals
            Append(program.Globals.Count);

            //constants
            Append(program.Constants.Count);
            for (int i = 0; i < program.Constants.Count; i++) {
                //don't use Append() because ToConstString adds a marker itself.
                bin.Append(program.Constants[i].ToConstString(M1));
            }

            //flags for non-minimized vm.
            int flags = (instructionStats ? 1 : 0) + (callStats ? 2 : 0);
            Append(flags);

            if (includeDebugData) {
                //export source code
                Append(program.SourceLines.Length);
                for (int i = 0; i < program.SourceLines.Length; i++) {
                    Append(program.SourceLines[i]);
                }

                Append(frames.Length);

                //export variable names and line numbers for instructions.
                for (int i = 0; i < frames.Length; i++) {
                    sws_Frame frame = frames[i];
                    Append(frame.Name);

                    //bytecode
                    Append(frame.Program.Count);
                    for (int j = 0; j < frame.Program.Count; j++) {
                        Append(frame.Program[j].Line);
                    }

                    //local variable names
                    Append(frame.Locals.Count);
                    for (int j = 0; j < frame.Locals.Count; j++) {
                        Append(frame.Locals[j].Name);
                    }

                    //upvalue variable names
                    Append(frame.Upvalues.Count);
                    for (int j = 0; j < frame.Upvalues.Count; j++) {
                        Append(frame.Upvalues[j].Name);
                    }
                }

                Append(program.Globals.Count);
                //export global variable names
                for (int i = 0; i < program.Globals.Count; i++) {
                    Append(program.Globals[i].Name);
                }
            }

            //convert to hex string

            string outputBinary = bin.ToString();

            StringBuilder output = new StringBuilder();

            for (int i = 0; i < outputBinary.Length; i++) {
                int c = outputBinary[i];
                output.Append(c.ToString("X2"));
            }

            return output.ToString();

            void Append(object value) {
                bin.Append(value.ToString());
                bin.Append(M1);
            }
        }

        /// <summary>
        /// Stormworks Lua VM stores program data in property boxes, which can only store 4096 characters each.
        /// </summary>
        /// <param name="program"></param>
        /// <returns></returns>
        public static string[] SplitProgramData(string program) {
            string[] output = new string[program.Length / 4096 + (program.Length % 4096 > 0 ? 1 : 0)];

            for (int i = 0; i < output.Length; i++) {
                if (i + 1 != output.Length) {
                    output[i] = program.Substring(i * 4096, 4096);
                } else {
                    //runs for last section, which isn't always 4096 characters.
                    output[i] = program.Substring(i * 4096);
                }
            }

            return output;
        }

        private static string FormatForXml(string text) {
            text = text.Replace("\r", ""); //just incase

            text = text.Replace("&", "&amp;");
            text = text.Replace("\"", "&quot;");
            text = text.Replace("'", "&apos;");
            text = text.Replace("<", "&lt;");
            text = text.Replace(">", "&gt;");

            return text;
        }
    }
}
