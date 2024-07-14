using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SWS.Compiler;

namespace SWS {
    /// <summary>
    /// Stores compiled SWScript program + other data needed to generate vehicle file.
    /// </summary>
    public class sws_Program {
        public string Source;
        public string[] SourceLines;
        public List<sws_Token> Tokens;

        public Dictionary<string, sws_Frame> StackFrames;
        public List<sws_Variable> Globals;
        public List<sws_Variable> Constants;

        public Dictionary<string, string> PropertyTexts;
        public Dictionary<string, double> PropertyNumbers;
        public Dictionary<string, bool> PropertyBools;

        public sws_Program(string source, string[] sourceLines, List<sws_Token> tokens, Dictionary<string, sws_Frame> stackFrames, List<sws_Variable> globals, List<sws_Variable> constants, Dictionary<string, string> propertyTexts, Dictionary<string, double> propertyNumbers, Dictionary<string, bool> propertyBools) {
            Source = source;
            SourceLines = sourceLines;
            Tokens = tokens;

            StackFrames = stackFrames;
            Globals = globals;
            Constants = constants;

            PropertyTexts = propertyTexts;
            PropertyNumbers = propertyNumbers;
            PropertyBools = propertyBools;
        }
    }
}
