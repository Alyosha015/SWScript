using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using static SWScript.compiler.sws_Compiler;
using static SWScript.compiler.sws_Opcode;

namespace SWScript.compiler {
    /// <summary>
    /// Stores compiled SWS program and contains many utility methods for the parser, especially for handling variables.
    /// </summary>
    internal class sws_Prg {
        public bool Compiled;

        public string Program;

        public string[] SourceLines;

        public Dictionary<string, sws_Frame> Frames;

        public Stack<sws_Frame> FrameStack;

        public sws_Frame Frame;

        /// <summary>
        /// Used to name functions with no name (ex. 'local testfunc = func() {}'. This is because you can't have a function with an integer as a name.
        /// </summary>
        public int UnnammedFuncCount = 0;

        //globals and constants are shared between all. Due to bytecode format they are both limited to 2^17 (131072) each.
        public List<sws_Variable> Globals;
        public List<sws_Variable> Constants;

        public sws_Prg(string[] sourceLines) {
            SourceLines = new string[sourceLines.Length];
            sourceLines.CopyTo(SourceLines, 0);

            Frames = new Dictionary<string, sws_Frame>();
            FrameStack = new Stack<sws_Frame>();
            
            Globals = new List<sws_Variable>();
            Constants = new List<sws_Variable>();
        }

        public void PushFrame(string name) {
            if (!Frames.ContainsKey(name)) {
                Frames.Add(name, new sws_Frame(name));
            }

            if (name != string.Empty) {
                Frame.Closures.Add(name);
                AddVariable(name, false, true);

                AddInstruction(op_closure, AddConst(name, sws_DataType.String));
                AddVarSetInstruction(name);

                //move instructions to start of program

                for (int i = 0; i < 2; i++) {
                    sws_Op instruction = LastInstruction();
                    RemoveLastInstruction();
                    AddInstructionToStart(instruction.Opcode, instruction.Data);
                }
            }

            Frame = Frames[name];

            FrameStack.Push(Frame);
        }

        public void PushNoName() {
            string name = UnnammedFuncCount.ToString();

            Frames.Add(name, new sws_Frame(name));

            Frame.Closures.Add(name);
            AddVariable(name, false, true);

            AddInstruction(op_closure, AddConst(name, sws_DataType.String));
            AddVarSetInstruction(name);

            Frame = Frames[name];

            FrameStack.Push(Frame);

            UnnammedFuncCount++;
        }

        public void AddLuaClosure(string name) {
            name = "lua " + name;

            Frame.Closures.Add(name);
            AddVariable(name, false, true);

            AddInstruction(op_closure, AddConst(name, sws_DataType.String));
            AddVarSetInstruction(name);

            //move instructions to start of program

            for (int i = 0; i < 2; i++) {
                sws_Op instruction = LastInstruction();
                RemoveLastInstruction();
                AddInstructionToStart(instruction.Opcode, instruction.Data);
            }
        }

        public void PopFrame() {
            if (FrameStack.Count == 1) {
                throw new sws_Error("Attempt to return from main.");
            }

            FrameStack.Pop();

            Frame = FrameStack.Peek();
        }

        public void AddInstruction(sws_Opcode opcode, int data = int.MinValue) {
            //Optimization: if opcode is op_add, check if one of two previous instructions is op_loadimm n, in which case it can be replaced with an op_addimm instruction.
            if (opcode == op_add) {
                sws_Op last = LastInstruction();
                if (last != null && last.Opcode == op_loadimm) {
                    int n = last.Data;
                    RemoveLastInstruction();
                    Frame.Program.Add(new sws_Op(op_addimm, n));
                    return;
                }

                sws_Op secondToLast = SecondToLastInstruction();
                if (secondToLast != null && secondToLast.Opcode == op_loadimm) {
                    int n = secondToLast.Data;
                    RemoveSecondToLastInstruction();
                    Frame.Program.Add(new sws_Op(op_addimm, n));
                    return;
                }
            }
            //Optimization: op_concat can work on multiple values in a similar way to op_tableset, by specifying the number to concat in the operand. Check if previous instruction is op_concat, and increment it's operand's value by 1 instead of adding this one.
            else if (opcode == op_concat) {
                sws_Op last = LastInstruction();
                if (last != null && last.Opcode == op_concat) {
                    last.Data++; //technically this has an edge case if you somehow go over the 2^17-1 limit for the operand, but that's never gonna happen anyway, right?.
                    return;
                }
            }

            Frame.Program.Add(new sws_Op(opcode, data));
        }

        public void AddInstructionToStart(sws_Opcode opcode, int data = int.MinValue) {
            Frame.Program.Insert(0, new sws_Op(opcode, data));
        }

        public sws_Op LastInstruction() {
            return Frame.Program.Last();
        }

        public sws_Op SecondToLastInstruction() {
            if (Frame.Program.Count > 1) {
                return Frame.Program[Frame.Program.Count - 2];
            }
            return null;
        }

        public void RemoveLastInstruction() {
            if (Frame.Program.Any()) {
                Frame.Program.RemoveAt(Frame.Program.Count - 1);
            }
        }

        public void RemoveSecondToLastInstruction() {
            if (Frame.Program.Count > 1) {
                Frame.Program.RemoveAt(Frame.Program.Count - 2);
            }
        }

        public void SetLocalScopeStart() {
            for (int i = 0; i < Frame.Locals.Count; i++) {
                sws_Variable local = Frame.Locals[i];
                if (local.Depth == sws_Parser.Depth && local.ScopeStart == -1) {
                    local.ScopeStart = Frame.Program.Count;
                }
            }
        }

        public void SetLocalScopeEnd() {
            for (int i = 0; i < Frame.Locals.Count; i++) {
                sws_Variable local = Frame.Locals[i];
                if (local.Depth == sws_Parser.Depth && local.ScopeEnd == -1) {
                    local.ScopeEnd = Frame.Program.Count;
                }
            }
        }

        public void AddLocal(string name) {
            AddVariable(name, true);
        }

        /// <summary>
        /// Adds a variable as either a local, upvalue, or global so it's index can later be retrieved.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="addAsLocal"></param>
        public void AddVariable(string name, bool addAsLocal=false, bool asGlobal=false) {
            if (asGlobal) {
                sws_Variable variable = GetGlobal(name);
                if (variable == null) {
                    Globals.Add(new sws_Variable().Global(name, Globals.Count));
                }
                return;
            }
            
            //force variable to be local
            if (addAsLocal) {
                sws_Variable local = GetLocal(name);
                
                if (local == null) {
                    Frame.Locals.Add(new sws_Variable().Local(name, Frame.Locals.Count, -1, sws_Parser.Depth));
                    return;
                }

                throw new sws_Error($"Local variable '{name}' already exists in scope.");
            } else {
                //if variable being local is not forced, still check and return if it exists as a local. If it doesn't continue.
                if(GetLocal(name) != null) {
                    return;
                }
            }

            //search for locals in functions under this one in depth which match in name. If a match is found the variable is added as an upvalue.
            sws_Variable upvalue = GetUpvalue(name);
            if (upvalue == null) {
                sws_Frame[] framesArray = FrameStack.ToArray(); //has to be reversed because making a copy of the stack reverses it.
                Stack<sws_Frame> frames = new Stack<sws_Frame>(framesArray.Reverse());
                frames.Pop();
                while (frames.Count > 0) {
                    sws_Frame frame = frames.Pop();
                    int localIndex = frame.GetLocal(name);
                    if (localIndex != -1) {
                        Frame.Upvalues.Add(new sws_Variable().Upvalue(name, Frame.Upvalues.Count, frame.Name, localIndex));
                        return;
                    }
                }
            }

            //if searching for upvalues fails, add the variable as a global.
            sws_Variable global = GetGlobal(name);
            if (global == null) {
                Globals.Add(new sws_Variable().Global(name, Globals.Count));
            }
        }

        public int AddConst(object value, sws_DataType dataType) {
            int index = GetConst(value, dataType);

            if (index == -1) {
                index = Constants.Count;
                Constants.Add(new sws_Variable().Constant(value, dataType, index));
            }

            return index;
        }

        /// <summary>
        /// Adds instruction to get variable with given name.
        /// </summary>
        /// <param name="name"></param>
        public void AddVarGetInstruction(string name) {
            sws_Variable variable = GetVariable(name);

            //if variable doesn't exist assume it's a global
            if (variable == null) {
                variable = new sws_Variable().Global(name, Globals.Count);
                Globals.Add(variable);
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
        public void AddVarSetInstruction(string name) {
            sws_Variable variable = GetVariable(name);

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

            //if variable doesn't exist assume it's a global
            if (variable == null) {
                variable = new sws_Variable().Global(name, Globals.Count);
                Globals.Add(variable);
                AddInstruction(op_setglobal, variable.Index);
                return;
            }
        }

        /// <summary>
        /// Get variable with given name. Priority is given to locals, than upvalues, than globals
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public sws_Variable GetVariable(string name) {
            sws_Variable variable = GetLocal(name);
            if(variable != null) {
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

        public int GetConst(object value, sws_DataType dataType) {
            for (int i = 0; i < Constants.Count; i++) {
                sws_Variable constant = Constants[i];
                
                if(constant.ValueType != dataType) {
                    continue;
                }

                switch(constant.ValueType) {
                    case sws_DataType.Null:
                        return i;
                    case sws_DataType.Bool:
                        if ((bool)value == (bool)constant.Value) {
                            return i;
                        }
                        break;
                    case sws_DataType.Double:
                        if((double)value == (double)constant.Value) {
                            return i;
                        }
                        break;
                    case sws_DataType.String:
                        if(value.ToString() == constant.Value.ToString()) {
                            return i;
                        }
                        break;
                }
            }
            return -1;
        }

        public sws_Variable GetLocal(string name) {            
            for (int i = 0; i < Frame.Locals.Count; i++) {
                sws_Variable local = Frame.Locals[i];
                //ScopeEnd being -1 means the variable is inside the scope of where the parser is currently.
                if (local.ScopeEnd == -1 && name == local.Name) {
                    return local;
                }
            }

            return null;
        }

        public sws_Variable GetUpvalue(string name) {
            for (int i = 0; i < Frame.Upvalues.Count; i++) {
                if (name == Frame.Upvalues[i].Name) {
                    return Frame.Upvalues[i];
                }
            }

            //if it doesn't exist, search for upvalue and add it if found.
            sws_Frame[] framesArray = FrameStack.ToArray(); //has to be reversed because making a copy of the stack reverses it.
            Stack<sws_Frame> frames = new Stack<sws_Frame>(framesArray.Reverse());
            frames.Pop();
            while (frames.Count > 0) {
                sws_Frame frame = frames.Pop();
                int localIndex = frame.GetLocal(name);
                if (localIndex != -1) {
                    Frame.Upvalues.Add(new sws_Variable().Upvalue(name, Frame.Upvalues.Count, frame.Name, localIndex));
                    return Frame.Upvalues.Last();
                }
            }

            return null;
        }

        public sws_Variable GetGlobal(string name) {
            for (int i = 0; i < Globals.Count; i++) {
                if (name == Globals[i].Name) {
                    return Globals[i];
                }
            }

            return null;
        }

        public int GetPC() {
            return Frame.Program.Count - 1;
        }

        public void ResolveJumpFlag(int flag, int offset = 0) {
            for (int i = 0; i < Frame.Program.Count; i++) {
                sws_Op instruction = Frame.Program[i];
                
                if (instruction.Data == flag) {
                    instruction.Data = Frame.Program.Count - i - 1 + offset;
                }
            }
        }

        public void ResolveJumpPC(int pc) {
            Frame.Program[pc].Data = Frame.Program.Count - pc - 1;
        }

        public void ResolveJumpPCs(List<int> pcs) {
            for (int i = 0; i < pcs.Count; i++) {
                ResolveJumpPC(pcs[i]);
            }
        }

        /// <summary>
        /// Optimizes when instructions such as op_eq and op_jfalse follow eachother into a single instruction.
        /// </summary>
        /// <param name="flag"></param>
        public void AddJumpFInstruction(int flag) {
            sws_Opcode last = LastInstruction().Opcode;
            switch(last) {
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

        public bool JumpFCanSimplify() {
            return Array.IndexOf(new sws_Opcode[] { op_eq, op_neq, op_lt, op_lte }, LastInstruction().Opcode) != -1;
        }

        public void ResolveContinueBreakLoop(List<int> instructionIndices, int continuePC, int breakPC) {
            for (int i = 0; i < instructionIndices.Count; i++) {
                int pc = instructionIndices[i];
                sws_Op instruction = Frame.Program[pc];

                if (instruction.Data == JMP_CONTINUE) {
                    instruction.Data = continuePC - pc;
                } else if (instruction.Data == JMP_BREAK) {
                    instruction.Data = breakPC - pc;
                }
            }
        }

        public void ResolveIfEnd(List<int> instructionIndices, int endPC) {
            for (int i = 0; i < instructionIndices.Count; i++) {
                int pc = instructionIndices[i];
                sws_Op instruction = Frame.Program[pc];

                if (instruction.Data == JMP_IFEND) {
                    instruction.Data = endPC - pc;
                }
            }
        }

        /// <summary>
        /// Export program in format stored as hex string. This is used as input for both the c# and lua vm.
        /// </summary>
        /// <returns></returns>
        public void Export() {
            StringBuilder outbin = new StringBuilder();

            char M1 = (char)1; //marker used at end of value

            List<sws_Frame> frames = Frames.Values.ToList();

            //frame data
            outbin.Append(frames.Count);
            outbin.Append(M1);

            for (int i = 0; i < frames.Count; i++) {
                sws_Frame frame = frames[i];

                //frame name
                outbin.Append(frame.Name);
                outbin.Append(M1);

                //bytecode
                outbin.Append(frame.Program.Count);
                outbin.Append(M1);

                for (int j = 0; j < frame.Program.Count; j++) {
                    outbin.Append(frame.Program[j].ExportToInt());
                    outbin.Append(M1);
                }

                //locals
                outbin.Append(frame.Locals.Count);
                outbin.Append(M1);

                //upvalues
                outbin.Append(frame.Upvalues.Count);
                outbin.Append(M1);

                for (int j = 0; j < frame.Upvalues.Count; j++) {
                    outbin.Append(frame.Upvalues[j].StackFrameReference);
                    outbin.Append(M1);
                    outbin.Append(frame.Upvalues[j].LocalIndexReference);
                    outbin.Append(M1);
                }
            }

            //globals
            outbin.Append(Globals.Count);
            outbin.Append(M1);

            //constants
            outbin.Append(Constants.Count);
            outbin.Append(M1);

            for (int i = 0; i < Constants.Count; i++) {
                outbin.Append(Constants[i].Value);
                outbin.Append(M1);
                outbin.Append((int)Constants[i].ValueType);
                outbin.Append(M1);
            }

            //convert to hexadecimal string
            string outputBinary = outbin.ToString();

            StringBuilder outhex = new StringBuilder();

            for (int i = 0; i < outputBinary.Length; i++) {
                int c = outputBinary[i];
                outhex.Append(c.ToString("X2"));
            }

            Program = outhex.ToString();
        }
    }
}
