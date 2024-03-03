using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SWScript.compiler;
using SWScript.vm;

namespace SWScript {
    internal class Program {
        private static readonly string StartMessage = @"********************************************************
   _____      ______        _      __     ___ ___   ___
  /###/#| /| /#/ __/_______(_)__  / /_   <  // _ \ / _ \
 _\#\ |#|/#|/#/\ \/ __/ __/ / _ \/ __/   / // // // // /
/###/ |##/|##/___/\__/_/ /_/ .__/\__/   /_(_)___(_)___/
                          /_/
Compiler & VM                                  v2024.3.2
********************************************************

Type 'help' for a list of commands.";

        private static readonly string HelpMessage = @"
****************** List of Commands ********************
help                      - shows this message.

sws <path> [-<options>]   - compiles program at <path> with options [-<options>] (Listed below).
   -run -r                - runs compiled program in C# interpreter (note: can't call lua functions).
   -source -s             - prints source code being compiled.
   -tokens -t             - prints tokens of program.
   -bytecode -b           - prints bytecode of compiled program.
   -output -o             - prints bytecode as hex string.
   -export -e             - exports program to stormworks as a vehicle file.
   -lbexport -lbe         - exports program as property sets for LifeBoatAPI.";

        static void Main(string[] args) {
            ANSIColor.EnableANSIInConsole();

            if (args.Length == 1 && File.Exists(args[0])) {
                sws_Compiler.SetSource(File.ReadAllText(args[0]));
                sws_Compiler.Compile();
                
                if (sws_Error.Count > 0) {
                    Console.WriteLine("\nStopped due to errors.");
                }

                Export();

                Run();

                Console.ReadKey();

                return;
            }

            Console.WriteLine(StartMessage);

            while (true) {
                Console.ForegroundColor = ConsoleColor.Yellow;

                Console.Write("\n>");
                
                Cmd command = new Cmd(Console.ReadLine());

                Console.ResetColor();

                if (command.CommandIs("sws")) {
                    if (!command.HasPath()) {
                        PrintError("Expected path for file to compile.");
                    }

                    if (!File.Exists(command.Path)) {
                        PrintError($"File at path '{command.Path}' does not exist.");
                        continue;
                    }

                    sws_Compiler.SetSource(File.ReadAllText(command.Path));

                    long startTime = DateTime.UtcNow.Ticks;
                    Console.Write($"\nCompiling...");
                    sws_Compiler.Compile();

                    if (sws_Error.Count > 0) {
                        Console.WriteLine("\nStopped due to errors.");
                        continue;
                    } else {
                        Console.WriteLine($" Finished in {(DateTime.UtcNow.Ticks - startTime) / 10000f} ms.");
                    }

                    if (command.HasOption("-source") || command.HasOption("-s")) {
                        sws_Compiler.PrintSource();
                    }
                    if (command.HasOption("-tokens") || command.HasOption("-t")) {
                        sws_Compiler.PrintTokens();
                    }
                    if (command.HasOption("-bytecode") || command.HasOption("-b")) {
                        sws_Compiler.PrintBytecode();
                    }
                    if (command.HasOption("-output") || command.HasOption("-o")) {
                        sws_Compiler.PrintOutput();
                    }
                    if (command.HasOption("-export") || command.HasOption("-e")) {
                        Export();
                    }
                    if (command.HasOption("-lbexport") || command.HasOption("-lbe")) {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("\n**** LifeBoatAPI Data ****");
                        Console.ResetColor();

                        string data = sws_Compiler.GenerateLifeBoatAPIData();

                        Console.WriteLine(data);
                    }
                    if (command.HasOption("-run") || command.HasOption("-r")) {
                        Run();
                    }
                } else if (command.Command == "help") {
                    Console.WriteLine(HelpMessage);
                } else if (command.Line != string.Empty) {
                    PrintError($"Unknown command '{command.Command}'.");
                }
            }
        }

        private static void PrintError(string error) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
        }

        private static void Export() {
            string vehicleFile = sws_Compiler.GenerateVehicleFile();
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Stormworks/data/vehicles/SWScript Export.xml";
            File.WriteAllText(path, vehicleFile);

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\nExported vehicle file.");
            Console.ResetColor();
        }

        private static void Run() {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n**** Run (ESC to stop) ****");
            Console.ResetColor();

            Thread vm = new Thread(new sws_VM(sws_Compiler.Prg).Run);

            long vmStartTime = Environment.TickCount;

            vm.Start();

            bool aborted = false;

            while (vm.IsAlive) {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) {
                    aborted = true;

                    vm.Abort();

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nProgram aborted.");
                    Console.ResetColor();
                    break;
                }

                Thread.Sleep(16);
            }

            if (!aborted) {
                long vmEndTime = Environment.TickCount;

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"\nFinished executing program in {vmEndTime - vmStartTime} ms.");
                Console.ResetColor();
            }
        }
    }
}
