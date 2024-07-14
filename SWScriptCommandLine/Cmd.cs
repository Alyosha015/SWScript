using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScriptCommandLine {
    internal class Cmd {
        public string Line;

        public string Command;

        public string Path;

        public List<string> Paths;

        public Dictionary<string, string> Options;

        public Cmd(string line) {
            Line = line.Trim();

            Paths = new List<string>();
            Options = new Dictionary<string, string>();

            Parse();
        }

        private void Parse() {
            int start = -1;
            int current = 0;

            while (current < Line.Length) {
                char c = Line[current++];

                if (char.IsWhiteSpace(c)) {
                    continue;
                }

                if (c == '"') {
                    Paths.Add(ParseQoutedStr());
                } else {
                    while (current < Line.Length && !char.IsWhiteSpace(Line[current])) {
                        current++;
                    }

                    string line = Line.Substring(start + 1, current - start - 1);
                    start = current;

                    if (line[0] != '-') {
                        if (Command == null) {
                            Command = line;
                        } else {
                            Paths.Add(line);
                        }
                    } else {
                        string option = line;
                        string value = string.Empty;
                        
                        if (line.Contains('=')) {
                            option = line.Substring(0, line.IndexOf('='));
                            value = line.Substring(option.Length + 1);
                        }

                        if (Options.ContainsKey(option)) {
                            Options[option] = value;
                        }

                        Options.Add(option, value);
                    }
                }
            }

            if (Paths.Count > 0) {
                Path = Paths[0];
            }

            string ParseQoutedStr() {
                while (true) {
                    if (current >= Line.Length) {
                        return Line.Substring(start + 2, current - start - 2);
                    }

                    if (Line[current] == '"' && Line[current - 1] != '\\') {
                        break;
                    }

                    current++;
                }

                current++;

                string str = Line.Substring(start + 2, current - start - 3);
                start = current;

                return str;
            }
        }

        public bool HasPath() {
            return Paths.Count > 0;
        }

        public bool CommandIs(string command) {
            return Command == command;
        }

        public bool HasOption(params string[] options) {
            foreach (string option in options) {
                if (Options.ContainsKey(option)) {
                    return true;
                }
            }

            return false;
        }

        public string GetValue(params string[] options) {
            string value = string.Empty;

            foreach(string option in options) {
                if (Options.ContainsKey(option)) {
                    value = Options[option];
                }
            }

            return value;
        }
    }
}
