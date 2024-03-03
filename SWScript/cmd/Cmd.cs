using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWScript {
    internal class Cmd {
        public string Line;

        public string Command;

        public string Path;

        public List<string> Paths;

        public List<string> Options;
        
        public Cmd(string line) {
            Line = line.Trim();
            
            Paths = new List<string>();
            Options = new List<string>();

            Parse();
        }

        private void Parse() {
            int start = -1;
            int current = 0;

            while (current < Line.Length) {
                char c = Line[current++];

                if(char.IsWhiteSpace(c)) {
                    continue;
                }

                if (c == '"') {
                    while(true) {
                        if (current >= Line.Length) {
                            return;
                        }

                        if (Line[current] == '"' && Line[current - 1] != '\\') {
                            break;
                        }

                        current++;
                    }

                    current++;

                    string path = Line.Substring(start + 2, current - start - 3);
                    start = current;

                    Paths.Add(path);
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
                        Options.Add(line);
                    }
                }
            }

            if (Paths.Count > 0) {
                Path = Paths[0];
            }
        }

        public bool HasPath() {
            return Paths.Count > 0;
        }

        public bool CommandIs(string command) {
            return Command == command;
        }

        public bool HasOption(string option) {
            return Options.Contains(option);
        }
    }
}
