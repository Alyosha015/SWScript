using SWScript.compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScript.vm {
    internal class sws_VMError : Exception {
        public sws_VMError(string message, sws_Prg prg, string frameName, int pc) {
            int line = prg.Frames[frameName].Program[pc].Line;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[SWS ERROR] @ line:{line} (func:{frameName}, pc:{pc}) {message}");
            
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"{prg.SourceLines[line - 1]}");
            
            Console.ResetColor();
        }
    }
}
