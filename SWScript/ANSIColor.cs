using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SWS {
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

    internal class AnsiColor {
        /// <summary>
        /// If enable is false empty strings are returned instead of the control characters.
        /// </summary>
        public static bool Enable;

        public static string ResetWriteColor => Enable ? "\x1B[0m" : string.Empty;

        public static string Color(FG fg, BG bg) {
            return Enable ? $"\x1B[{(int)fg};{(int)bg}m" : string.Empty;
        }

        public static string Color(FG fg) {
            return Enable ? $"\x1B[{(int)fg}m" : string.Empty;
        }
    }
}
