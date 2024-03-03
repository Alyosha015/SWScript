using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SWScript {
    internal enum FG {
        Black = 30,
        Red = 31,
        Green = 32,
        Yellow = 33,
        Blue = 34,
        Magenta = 35,
        Cyan = 36,
        White = 37,
        Gray = 90,
        BrightRed = 91,
        BrightGreen = 92,
        BrightYellow = 93,
        BrightBlue = 94,
        BrightMagenta = 95,
        BrightCyan = 96,
        BrightWhite = 97,
    }

    internal enum BG {
        Black = 40,
        Red = 41,
        Green = 42,
        Yellow = 43,
        Blue = 44,
        Magenta = 45,
        Cyan = 46,
        White = 47,
        Gray = 100,
        BrightRed = 101,
        BrightGreen = 102,
        BrightYellow = 103,
        BrightBlue = 104,
        BrightMagenta = 105,
        BrightCyan = 106,
        BrightWhite = 107,
    }

    internal class ANSIColor {
        public static readonly string ResetWriteColor = "\x1B[0m";

        public static string Color(FG fg, BG bg) {
            return $"\x1B[{(int)fg};{(int)bg}m";
        }

        public static string Color(FG fg) {
            return $"\x1B[{(int)fg}m";
        }

        const int STD_OUTPUT_HANDLE = -11;
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        /// <summary>
        /// Use to enable using ANSI color codes in console.
        /// </summary>
        public static void EnableANSIInConsole() {

            IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);

            if (hConsole != IntPtr.Zero) {
                if (GetConsoleMode(hConsole, out uint mode)) {
                    mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                    SetConsoleMode(hConsole, mode);
                }
            }
        }
    }
}
