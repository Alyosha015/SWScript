using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScript.compiler {
    internal class sws_Compiler {
        public static readonly int JMPFLAG = 262144; //2^18
        //space between these flags reserved for bytecode generator handling bool and/or (which is implemented using conditional jumps to have short-circuiting)
        public static readonly int JMP_CONDITION = 524288; //2^19
        public static readonly int JMP_CONTINUE = 524288 + 1;
        public static readonly int JMP_BREAK = 524288 + 2;
        public static readonly int JMP_IFEND = 524288 + 3;

        public static string Source;
        public static string[] SourceLines;

        public static List<sws_Token> FirstPassTokens;
        public static List<sws_Token> Tokens;

        public static sws_Prg Prg;

        public static void SetSource(string source) {
            Source = source;
            SourceLines = Source.Replace("\n", "\n").Split('\n');
        }

        /// <summary>
        /// Compiles source text to SWS Bytecode. Compilation is done in 2 steps, tokenizing the source file, and then parsing it directly into bytecode. Unlike many languages, no intermediate representation of the program is used between parsing and creating bytecode. Tokenization is itself done in two passes, in the first the source is completely tokenized, however the second combines some tokens which represent larger operations into a single token.
        /// 
        /// </summary>
        public static void Compile() {
            if(Source == null) {
                return;
            }

            sws_Error.Reset();

            sws_TokenizerFirstPass.Tokenize();
            sws_TokenizerSecondPass.Tokenize();

            sws_Parser.Parse();
        }

        public static void PrintSource() {
            if(Source == null) {
                return;
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n****** Source Code Printout ({Source.Length} bytes) ******");
            Console.ResetColor();
            Console.WriteLine(Source);
        }

        public static void PrintTokens() {
            if (Tokens == null) {
                return;
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n****** Token Printout ({Tokens.Count} Tokens) ******");
            Console.ResetColor();
            Console.WriteLine($"{ANSIColor.Color(FG.BrightWhite)}    #  Type                               Ln / Col: Literal");

            for (int i = 0; i < Tokens.Count; i++) {
                sws_Token token = Tokens[i];

                Console.WriteLine($"{new string(' ', 5 - i.ToString().Length)}{ANSIColor.Color(FG.Cyan)}{i}  {ANSIColor.Color(FG.BrightWhite)}{token.TokenType}{ANSIColor.Color(FG.White)}{new string(' ', 32 - token.TokenType.ToString().Length)}{new string(' ', 5 - token.NLine.ToString().Length)}{token.NLine} / {new string(' ', 3 - token.NColumn.ToString().Length)}{token.NColumn}: {ANSIColor.Color(FG.BrightWhite)}{token.Literal}{ANSIColor.ResetWriteColor}");
            }
        }

        public static void PrintBytecode() {
            if (Prg == null) {
                return;
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n****** Bytecode Printout ******");
            Console.ResetColor();

            sws_Frame[] frames = Prg.Frames.Values.ToArray();

            for (int i = 0; i < frames.Length; i++) {
                if (i != 0) {
                    Console.WriteLine("* * * * * * * * * * * * * * * *");
                }

                sws_Frame frame = frames[i];

                //print bytecode
                string funcName = frame.Name;
                //functions declared without a name are given an id number internally (which is otherwise an impossible name).
                if (funcName.Length > 0 && funcName[0] >= '0' && funcName[0] <= '9') {
                    funcName = $"(id = {frame.Name})";
                }
                Console.WriteLine($"func {funcName}() {'{'}");

                List<sws_Op> bytecode = frame.Program;

                for (int j = 0; j < bytecode.Count; j++) {
                    sws_Op op = bytecode[j];

                    string line = $"{new string(' ', 5 - j.ToString().Length)}{j}  {op.Opcode} {(op.Data == int.MinValue ? string.Empty : op.Data.ToString())}";

                    if (op.IsJumpOp()) {
                        line += $"{new string(' ', 30 - line.ToString().Length)}";

                        Console.WriteLine($"{line}{ANSIColor.Color(FG.Gray)};JUMP TO PC:{j + op.Data + 1}{ANSIColor.ResetWriteColor}");
                    } else if (op.IsDataOp()) {
                        line += $"{new string(' ', 30 - line.ToString().Length)}";

                        if (op.Opcode == sws_Opcode.op_getconst) {
                            Console.WriteLine($"{line}{ANSIColor.Color(FG.Gray)};{Prg.Constants[op.Data].Value} ({Prg.Constants[op.Data].ValueType}){ANSIColor.ResetWriteColor}");
                        } else if (op.Opcode == sws_Opcode.op_getlocal || op.Opcode == sws_Opcode.op_setlocal) {
                            Console.WriteLine($"{line}{ANSIColor.Color(FG.Gray)};{frame.Locals[op.Data].Name}{ANSIColor.ResetWriteColor}");
                        } else if (op.Opcode == sws_Opcode.op_getupval || op.Opcode == sws_Opcode.op_setupval) {
                            Console.WriteLine($"{line}{ANSIColor.Color(FG.Gray)};{frame.Upvalues[op.Data].Name}{ANSIColor.ResetWriteColor}");
                        } else if (op.Opcode == sws_Opcode.op_getglobal || op.Opcode == sws_Opcode.op_setglobal) {
                            Console.WriteLine($"{line}{ANSIColor.Color(FG.Gray)};{Prg.Globals[op.Data].Name}{ANSIColor.ResetWriteColor}");
                        }
                    } else if (op.Opcode == sws_Opcode.op_closure) {
                        line += $"{new string(' ', 30 - line.ToString().Length)}";

                        Console.WriteLine($"{line}{ANSIColor.Color(FG.Gray)};{Prg.Constants[op.Data].Value}{ANSIColor.ResetWriteColor}");
                    } else {
                        Console.WriteLine($"{line}");
                    }
                }

                Console.WriteLine("}\n");

                //print local variables
                List<sws_Variable> locals = frame.Locals;

                Console.WriteLine($"Locals ({locals.Count})");

                for (int j = 0; j < locals.Count; j++) {
                    sws_Variable local = locals[j];
                    Console.WriteLine($"{new string(' ', 5 - j.ToString().Length)}{j}  {local.Name}  {local.ScopeStart}, {local.ScopeEnd}, {local.Depth}");
                }

                Console.WriteLine();

                //print upvalues
                List<sws_Variable> upvalues = frame.Upvalues;

                Console.WriteLine($"Upvalues ({upvalues.Count})");

                for (int j = 0; j < upvalues.Count; j++) {
                    sws_Variable upvalue = upvalues[j];
                    Console.WriteLine($"{new string(' ', 5 - j.ToString().Length)}{j}  {upvalue.Name}  {upvalue.StackFrameReference}, {upvalue.LocalIndexReference}");
                }

                Console.WriteLine();

                //print closures
                List<string> closures = frame.Closures;

                Console.WriteLine($"Closures ({closures.Count})");

                for (int j = 0; j < closures.Count; j++) {
                    string closure = closures[j];
                    Console.WriteLine($"{new string(' ', 5 - j.ToString().Length)}{j}  {closure}");
                }

                Console.WriteLine();
            }

            //print globals
            Console.WriteLine($"**** Globals ({Prg.Globals.Count}) ****");

            for(int i=0;i<Prg.Globals.Count;i++) {
                sws_Variable global = Prg.Globals[i];
                Console.WriteLine($"{new string(' ', 5 - i.ToString().Length)}{i}  {global.Name}");
            }

            Console.WriteLine();

            //print constants
            Console.WriteLine($"**** Constants ({Prg.Constants.Count}) ****");

            for (int i = 0; i < Prg.Constants.Count; i++) {
                sws_Variable constant = Prg.Constants[i];
                Console.WriteLine($"{new string(' ', 5 - i.ToString().Length)}{i}  {constant.Value} ({constant.ValueType})");
            }
        }

        public static void PrintOutput() {
            if (!Prg.Compiled) {
                return;
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n****** Output Printout ({Prg.Program.Length} bytes) ******");
            Console.ResetColor();

            Console.WriteLine(Prg.Program);
        }

        public static string[] GenerateProgramData() {
            string[] data = new string[Prg.Program.Length / 4096 + (Prg.Program.Length % 4096 > 0 ? 1 : 0)];

            for (int i = 0; i < data.Length; i++) {
                if (i + 1 != data.Length) {
                    data[i] = Prg.Program.Substring(i * 4096, 4096);
                } else {
                    //runs for last section, which isn't always 4096 characters.
                    data[i] = Prg.Program.Substring(i * 4096);
                }
            }

            return data;
        }

        public static string GenerateLifeBoatAPIData() {
            StringBuilder output = new StringBuilder();

            string[] data = GenerateProgramData();

            output.AppendLine($"simulator:setProperty(\"c\", {data.Length})");

            for (int i = 0; i < data.Length; i++) {
                output.Append($"simulator:setProperty(\"p{i + 1}\", \"{data[i]}\")");
                
                if (i + 1 < data.Length) {
                    output.Append("\n");
                }
            }

            return output.ToString();
        }

        private static readonly string VEHICLE_FILE_0 = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><vehicle data_version=\"3\" bodies_id=\"2\"><authors/><bodies><body unique_id=\"2\"><components><c d=\"microprocessor\"><o r=\"1,0,0,0,1,0,0,0,1\" sc=\"6\"><microprocessor_definition name=\"SWScript\" width=\"1\" length=\"1\" id_counter=\"4\" id_counter_node=\"0\"><nodes/><group><data><inputs/><outputs/></data><components><c type=\"56\"><object id=\"1\" script='local f,t=false,true\nlocal FC,c,Frms,F,n,h,st,S,CS,sp,cs,G,C,ot,od,PG,PRG,tn,ts,s=0,property.getNumber(\"c\"),{},{},\"\",f,f,{},{},0,0,{},{},f,f,\"\",\"\",tonumber,tostring,string\nfor i=1,c do PG=PG..property.getText(\"p\"..i)end c=1\nfunction NT()local j=0 local i=PRG:sub(c):find(s.char(1))if i~=nil then j=PRG:sub(c,c+i-2)c=c+i return j end return 0 end\nfor i=1,#PG/2 do PRG=PRG..s.char(tn(PG:sub(i*2-1,i*2),16))end\nFC=tn(NT())for i=1,FC do\nlocal frm={\"\",{},{},{},{},f,1,{},f}frm[1]=NT()local pc=tn(NT())for j=1,pc do\nfrm[2][j]=tn(NT())end\nNT()local uc=tn(NT())for j=0,uc-1 do frm[4][j]=NT()frm[5][j]=tn(NT())end\nFrms[frm[1]]=frm end\nNT()local cc=tn(NT())for i=0,cc-1 do\nC[i]=NT()NT()end\not=nil~=Frms[\"onTick\"]od=Frms[\"onDraw\"]~=nil\nfunction THas(tb,val)if tb[0]==val then return t end for k,v in ipairs(tb)do if v==val then return t end end return f end\nfunction Copy(a)local Frm={a[1],a[2],{},a[4],a[5],a[6],a[7],a[8],a[9]}for i=0,#a[3]do Frm[3][i]=a[3][i]end return Frm end\nfunction Exec(otc)h=f\nif not st then st=t elseif otc and ot then n=\"onTick\"elseif not otc and od then n=\"onDraw\"else return end\nF=Copy(Frms[n])if n~=\"\"then CS[1]=Copy(F)if THas(F[4],CS[0][1])then F[8][CS[0][1]]=0 end end\nwhile true do\nlocal PC=1+F[7]local i=F[2][PC-1]local I,D,E,NN,a,b,c=i&gt;&gt;18,i&amp;131071,0,t,0,0,0\nE=D\nif(i&amp;131072)&gt;0 then D=-D NN=f end\nif I&lt;22 then sp=sp-2 a,b=S[sp+1],S[sp]elseif I&lt;37 then sp=sp-1 a=S[sp]end\nif I==38 then c=F[3][D]\nelseif I==39 then c=CS[F[8][F[4][D]]][3][F[5][D]]\nelseif I==40 then c=G[D]\nelseif I==22 then F[3][D]=a\nelseif I==23 then CS[F[8][F[4][D]]][3][F[5][D]]=a\nelseif I==24 then G[D]=a\nelseif I==45 then S[sp-1]=D+S[sp-1]\nelseif I==41 then c=D\nelseif I==0 then c=a+b\nelseif I==1 then c=a-b\nelseif I==2 then c=a*b\nelseif I==3 then c=a/b\nelseif I==13 then c=a==b\nelseif I==14 then c=a~=b\nelseif I==15 then c=a&lt;b\nelseif I==16 then c=a&lt;=b\nelseif I==17 then c=b[a]\nelseif I==46 then PC=PC+D\nelseif I==18 then if a==b then PC=PC+D end\nelseif I==19 then if a~=b then PC=PC+D end\nelseif I==20 then if a&gt;b then PC=PC+D end\nelseif I==21 then if a&gt;=b then PC=PC+D end\nelseif I==26 then if not a then PC=PC+D end\nelseif I==27 then if a then PC=PC+D end\nelseif I==28 then if not a then PC=PC+D sp=sp+1 end\nelseif I==29 then if a then PC=PC+D sp=sp+1 end\nelseif I==49 then sp=sp-1 local tb=S[sp]for j=1,E do sp=sp-2 tb[S[sp+1]]=S[sp]end if D&lt;0 then S[sp]=tb sp=sp+1 end\nelseif I==42 then c=D&gt;0 and t or f\nelseif I==31 then c=-a\nelseif I==32 then c=not a\nelseif I==35 then c={}for k,v in pairs(a)do c[#c+1]=k end\nelseif I==36 then c=type(a)\nelseif I==34 then c=#a\nelseif I==50 then sp=sp-1 local cl=Copy(S[sp])cl[9]=NN if cl[6]then local out,arg={},{}for j=1,E do sp=sp-1 arg[j]=S[sp]end out=table.pack(cl[2](table.unpack(arg)))if D&gt;0 then for j=1,#out do S[sp]=out[j]sp=sp+1 end end else for j=0,E-1 do sp=sp-1 cl[3][j]=S[sp]end CS[cs]=Copy(F)cs=cs+1 for j=cs-1,0,-1 do if THas(cl[4],CS[j][1])then cl[8][CS[j][1]]=j end end F[7]=PC PC=1 F=Copy(cl)end\nelseif I==51 then cs=cs-1 if cs==1 and(ot or od)and n~=\"\"then h=t end if not F[9]then sp=sp-D end F=Copy(CS[cs])PC=1+F[7]\nelseif I==12 then c=ts(a)..ts(b)for j=1,D do sp=sp-1 c=c..ts(S[sp])end\nelseif I==4 then c=a//b\nelseif I==5 then c=a^b\nelseif I==6 then c=a%b\nelseif I==33 then c=~a\nelseif I==7 then c=a&amp;b\nelseif I==8 then c=a|b\nelseif I==9 then c=a~b\nelseif I==10 then c=a&lt;&lt;b\nelseif I==11 then c=a&gt;&gt;b\nelseif I==44 then c={}\nelseif I==37 then c=C[D]\nelseif I==43 then c=nil\nelseif I==25 then S[sp],S[sp+1]=a,a sp=sp+2\nelseif I==48 then h=t\nelseif I==30 then debug.log(a)\nelseif I==47 then local cln,cl,fn=C[D],{},_ENV cl[1]=cln cl[3]={}cl[6]=cln:find(\"lua \",1,true)~=nil if cl[6]then cln=cln:gmatch(\"%S+\")cln()for v in cln do fn=fn[v]end cl[2]=fn else cl=Copy(Frms[cln])end S[sp]=cl sp=sp+1 end\nif I&lt;18 or(I&gt;30 and I&lt;45)then S[sp]=c sp=sp+1 end\nF[7]=PC\nif h then break end\nend\nif not ot and not od then return else if n==\"\"then CS[0]=Copy(F)end cs=2 end end\nfunction onTick()Exec(t)end\nfunction onDraw()Exec(f)end'><pos x=\"-0.5\" y=\"0.25\"/></object></c><c type=\"34\"><object id=\"2\" n=\"c\"><pos x=\"-0.5\" y=\"-0.5\"/><v text=\"";
        private static readonly string VEHICLE_FILE_1 = "\" value=\"";
        private static readonly string VEHICLE_FILE_2 = "\"/></object></c>";
        private static readonly string VEHICLE_FILE_3 = "</components><components_bridge/><groups/></group></microprocessor_definition><vp y=\"1\"/><logic_slots/></o></c></components></body></bodies><logic_node_links/></vehicle>";

        public static string GenerateVehicleFile() {
            string[] data = GenerateProgramData();

            StringBuilder output = new StringBuilder();

            output.Append(VEHICLE_FILE_0);
            output.Append(data.Length + VEHICLE_FILE_1 + data.Length + VEHICLE_FILE_2);

            for (int i = 0; i < data.Length; i++) {
                output.Append($"<c type=\"58\"><object id=\"4\" n=\"p{i+1}\" v=\"{data[i]}\"><pos x=\"-0.5\" y=\"-1.25\"/></object></c>");
            }

            output.Append(VEHICLE_FILE_3);

            return output.ToString();
        }
    }
}
