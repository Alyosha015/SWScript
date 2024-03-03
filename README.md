# SWScript
A programming language which can run in Stormworks Lua, allowing bypassing the character limit and writing very large programs.

Also a quick disclaimer, this isn't a practical solution to the Lua character limit, as running SWScript has huge performance penalties compared to Lua. A SWScript program is possibly hundreds of times slower, but in most cases the performance difference is by less than a hundred times.

## Tutorial
1. Download the latest release from the "Releases" section on the right vertical bar.

2. Write a program in a text file, and open SWScript.exe. Use the command `sws C:\your\path\to\the\file` to compile it. This won't do anything useful on it's own, but adding the `-e` option to the end (`sws C:\your\path\to\the\file -e`) will make the program generate a Stormworks file with the program and save it to your vehicle folder as `SWScript Export.xml`. For a full list of options run the command `help`.

## Documentation
I haven't had the time to write documentation for SWScript yet, for both the syntax/features of the language itself and the bytecode interpreter.

For now I suggest looking in the demo directory for some example programs I wrote, which hopefully is enough to learn how to write programs in this language.

There is one note I want to make about the `print` / `println` commands, these output data using `debug.log` in Lua, which needs to be viewed with something such as [DebugView](https://learn.microsoft.com/en-us/sysinternals/downloads/debugview).