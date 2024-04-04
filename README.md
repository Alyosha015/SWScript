# SWScript
A programming language which can run in Stormworks Lua, allowing bypassing the character limit and writing very large programs.

Also a quick disclaimer, this isn't a practical solution to the Lua character limit, as running SWScript has huge performance penalties compared to Lua. A SWScript program is possibly hundreds of times slower, but in most cases the performance difference is by less than a hundred times.

## Tutorial
1. Download the latest release from the "Releases" section on the right vertical bar.

2. Write a program in a text file, and open SWScript.exe. Use the command `sws C:\your\path\to\the\file` to compile it. This won't do anything useful on it's own, but adding the `-e` option to the end (`sws C:\your\path\to\the\file -e`) will make the program generate a Stormworks file with the program and save it to your vehicle folder as `SWScript Export.xml`
```
sws <path> [-<options>]   - compiles program at <path> with options [-<options>] (Listed below).
   -run -r                - runs compiled program in C# interpreter (note: can't call lua functions).
   -source -s             - prints source code being compiled.
   -tokens -t             - prints tokens of program.
   -bytecode -b           - prints bytecode of compiled program.
   -output -o             - prints bytecode as hex string.
   -export -e             - exports program to stormworks as a vehicle file.
   -lbexport -lbe         - exports program as property set commands for LifeBoatAPI.
```

## Documentation
I haven't had the time to write documentation for SWScript yet, for both the syntax/features of the language itself and the bytecode interpreter.

For now I suggest looking in the demo directory for some example programs I wrote, which hopefully is enough to learn how to write programs in this language.

There is one note I want to make about the `print` / `println` commands, these output data using `debug.log` in Lua, which needs to be viewed with something such as [DebugView](https://learn.microsoft.com/en-us/sysinternals/downloads/debugview).

## Example Program
Taken from \demo\starfield.txt.
```
local rng = lua.math.random

local data, count, speed = {}, 100, 0.075

;9x5 monitor
local w, h = 288, 160

for i=0, i<count {
    data[i] = Star()
}

func Star() {
    return {
        x = rng(-w/2, w/2), ;downside of only returning integers (less possible star directions), and can can return 0 making the star not move.
        y = rng(-h/2, h/2),
        z = rng(1, 5),
        color = 0
    }
}

func onTick() {
    for i=0, i<count {
        local star = data[i]
        
        if star.color < 255 {
            star.color++
        }

        star.x += (star.x / star.z) * speed
        star.y += (star.y / star.z) * speed

        if star.x > w/2 || star.x < -w/2 || star.y > h/2 || star.y < -h/2 {
            data[i] = Star()
        }
    }
}

func onDraw() {
    for i=0, i<count {
        local star = data[i]
        
        lua.screen.setColor(star.color, star.color, star.color)

        lua.screen.drawLine(star.x + w/2, star.y + h/2, star.x + w/2, star.y + h/2 + 1)
    }
}
```