using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

using SWS;

namespace SWScriptCommandLine {
    internal class Program {
        private static readonly string StartMessage = @"********************************************************
   _____      ______        _      __     ___ ___ ___
  /###/#| /| /#/ __/_______(_)__  / /_   <  /<  // _ \
 _\#\ |#|/#|/#/\ \/ __/ __/ / _ \/ __/   / / / // // /
/###/ |##/|##/___/\__/_/ /_/ .__/\__/   /_(_)_(_)___/
                          /_/
Compiler & VM                                v2024.07.12
********************************************************

Type 'help' for a list of commands.";

        private static readonly string HelpMessage = @"
****************** List of Commands ********************
help                        - shows this message.

sws <path> [-<options>]     - compiles program at <path> with options [-<options>] (Listed below).
   -run -r                  - runs compiled program in SW-Lua simulator.
      -stats                - counts how many of each instruction is executed and each function is called.
      -fast                 - runs using VM with no debug overhead.
      -w=<width>            - set width of monitor simulation in blocks. (Default 1)
      -h=<height>           - set height of monitor simulation in blocks. (Default 1)
   -tokens -t               - prints tokens of program.
   -bytecode -b             - prints bytecode of compiled program.
   -export -e               - exports program to Stormworks as a vehicle file.
   -lbexport -lbe           - exports program as property set commands for LifeBoatAPI.
   -debug -db               - used with -lbexport, add data used for debuging VM to program.
   -vm=<path>               - used with -run and -export, contents of file are used as vm instead of default.";

        private static SWScript swscript;
        private static sws_Program program;

        static void Main(string[] args) {
            AppDomain.CurrentDomain.ProcessExit += OnExit;

            ANSIColor.EnableAnsiInConsole();

            swscript = new SWScript();

            Console.WriteLine(StartMessage);

            while (true) {
                Console.ForegroundColor = ConsoleColor.Yellow;

                Console.Write("\n>");

                Cmd command = new Cmd(Console.ReadLine());

                Console.ResetColor();

                if (command.CommandIs("sws")) {
                    if (!command.HasPath()) {
                        PrintError("Expected path for file to compile.");
                        continue;
                    }

                    if (!File.Exists(command.Path)) {
                        PrintError($"File at path '{command.Path}' does not exist.");
                        continue;
                    }

                    Stopwatch compilerStopwatch = Stopwatch.StartNew();
                    Console.Write($"\nCompiling...");

                    program = swscript.Compile(File.ReadAllText(command.Path));

                    Console.WriteLine($" Finished in {compilerStopwatch.Elapsed.TotalMilliseconds} ms.");
                    compilerStopwatch.Stop();

                    if (command.HasOption("-tokens", "-t")) {
                        PrintHeader("**** Token Data ****");
                        string data = swscript.TokenPrintout(true);
                        Console.Write(data);
                    }
                    if (command.HasOption("-bytecode", "-b")) {
                        PrintHeader("**** Bytecode Printout ****");
                        string data = swscript.BytecodePrintout(true);
                        Console.Write(data);
                    }
                    if (command.HasOption("-export", "-e")) {
                        if(!Command_VM(command, out string vm)) {
                            continue;
                        }

                        string data = swscript.ExportToSW(vm);

                        string fileName = "SWScript Export.xml";

                        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Stormworks\data\vehicles\" + fileName);

                        //no \r in file
                        StreamWriter sw = new StreamWriter(path);
                        sw.Write(data);
                        sw.Close();

                        PrintHeader($"Exported as '{fileName}'.");
                    }
                    if (command.HasOption("-lbexport", "-lbe")) {
                        PrintHeader("**** LifeBoatAPI Data ****");

                        string data = swscript.ExportToLB(command.HasOption("-debug", "-db"), command.HasOption("-stats"), command.HasOption("-stats"));

                        Console.WriteLine(data);
                    }
                    if (command.HasOption("-run", "-r")) {
                        int w = 0, h = 0;

                        if (command.HasOption("-w") && !int.TryParse(command.GetValue("-w"), out w)) {
                            PrintError($"Unable to parse value of width argument '{command.GetValue("-w")}'.");
                        }

                        if (command.HasOption("-h") && !int.TryParse(command.GetValue("-h"), out h)) {
                            PrintError($"Unable to parse value of height argument '{command.GetValue("-h")}'.");
                        }

                        if (!Command_VM(command, out string vm)) {
                            continue;
                        }

                        PrintHeader("**** Running Program ****");

                        swscript.Run(false, command.HasOption("-fast"), !command.HasOption("-fast"), command.HasOption("-stats"), command.HasOption("-stats"), w, h, vm);
                    }
                } else if (command.CommandIs("help")) {
                    Console.WriteLine(HelpMessage);
                } else if (command.Line != string.Empty) {
                    PrintError($"Unknown command '{command.Command}'.");
                }
            }
        }

        //For some reason the console window takes a few seconds to close if the monogame window is ran at any point.
        private static void OnExit(object sender, EventArgs e) {
            Process.GetCurrentProcess().Kill();
        }

        private static bool Command_VM(Cmd command, out string vm) {
            vm = string.Empty;
            
            if (command.HasOption("-vm")) {
                string vmPath = command.GetValue("-vm");
                if (File.Exists(vmPath)) {
                    vm = File.ReadAllText(vmPath);
                } else {
                    PrintError($"Unable to find file at path '{vmPath}'. (for -vm command)");
                    return false;
                }
            }

            return true;
        }

        private static void PrintError(string error) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
        }

        private static void PrintHeader(string header) {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n{header}");
            Console.ResetColor();
        }
    }
}
