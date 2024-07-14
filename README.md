# SWScript
A programming language which can run in Stormworks Lua, allowing bypassing the character limit and writing very large programs.

Also a quick disclaimer, this isn't a practical solution to the Lua character limit, I made it for fun and not as a serious solution to the problem. A program written in SWScript can easily be hundreds of times slower than the equivalent Lua.

## Tutorial
1. Download the latest release from the "Releases" section on the right vertical bar.

2. Write a program in a file, and open the exe. Use the command `sws C:\your\path\to\the\file` to compile it. This won't do anything useful on it's own, but adding the `-e` option (`sws C:\your\path\to\the\file -e`) will make the program generate a Stormworks file with the program and save it to your vehicle folder as `SWScript Export.xml`. To run the program use `-r`. For the full list of commands check the documentation below or use the command `help`.

The `Demos` directory contains example programs.

# SWScript Documentation
## Table of Contents
1. [Command Line Compiler](#command-line-compiler)
    1. [Commands](#commands)
    2. [Debugging](#debugging)
2. [VSCode Syntax Highlighting](#vscode-syntax-highlighting)
3. [The Language](#the-language)
    1. [Program Execution](#program-execution)
    2. [Syntax](#syntax)
        1. [Keywords](#keywords)
        2. [Comments](#comments)
        3. [Null](#null)
        4. [Numbers](#numbers)
        5. [Strings](#strings)
        6. [Variables](#variables)
    3. [Operators](#operators)
        1. [Regular Operators](#regular-operators)
        2. [Assignment Operators](#assignment-operators)
        3. [Short Circuit Evaluation](#short-circuit-evaluation)
        4. [Truthy / Falsy Values](#truthy--falsy-values)
    4. [Statements](#statements)
        1. [Multi-Variable Assignment](#multi-variable-assignment)
        2. [If Statement](#if-statement)
        3. [Switch Statemenet](#switch-statement)
        4. [For Loop](#for-loop)
        5. [While Loop](#while-loop)
        6. [Loop Control](#loop-control)
        7. [Functions](#functions)
        8. [Return](#return)
        9. [@property](#property)
        10. [import](#import)
    5. [Tables](#tables)
    6. [Function Calls](#function-calls)
        1. [Self-Call](#self-call)
        2. [Lua-Call](#calling-lua-functions)
    7. [Built-In Functions](#built-in-functions)
        1. [print/println](#print--println)
        2. [_keys()](#keys)
        3. [_type()](#type)
4. [Bytecode & VM](#bytecode--vm)
    1. [Instruction Set](#instruction-set)
    2. [Compiling](#compiling)

# Command Line Compiler
The SWScript compiler is a command line tool, it allows compiling the programs, running them, and exporting them to Storkworks. There are also a few extra features such as being able to view the bytecode of generated programs.

## Commands
```
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
   -vm=<path>               - used with -run and -export, contents of file are used as vm instead of default.
```

## Debugging
When using the `-run` option, internally a version of the VM written in Lua is ran. The error messages it can give are very limited, some basic type checking is done, and there's a stack trace / line numbers for the error.

# VSCode Syntax Highlighting

To make programming a bit easier, I made a VSCode extension that adds syntax highlighting for SWScript (.sws) files.

To add it to VSCode open the Extension sidebar, click on the three dots (top right of sidebar), select `Install From VSIX` in the dropdown, and select the .vsix file that came with the download. Otherwise the unpackaged extension is in the `swscript-syntax-highlighting` directory.

# The Language
Overall, SWScript is very similar to Lua, the main differences being braces everywhere and more operators.

## Program Execution
The functions onTick and onDraw are equivalent to Lua's onTick and onDraw, and the outside "body" of the program will be executed once before either.

## Syntax
### Keywords
Below are all keywords reserved by the language:
```
import
lua
if
else
else if
for
while
continue
switch
intswitch
case
default
break
func
return
local
true
false
null
print
println
@property
```

### Comments
Comments are single line only and begin with a semicolon.

### Null
`null` is the same as Lua's `nil`.

### Numbers

A number can contain any characters from 0-9, underscores, and a decimal point. They can't start with underscores, or else they would be considered a variable. Aside from decimal, numbers can also be declared in hexadecimal or binary. A hexadecimal number has to start with 0x, and binary with 0b. These can't have a decimal point.
```
123
1_000_000
123.456
0xFFFF
0b1111_1111_1111_1111
```

### Strings
Strings are declared with either single or double quotes.

Escape characters are parsed in strings, all supported are listed below:
```
\\ \" \' \0 \a \b \f \n \r \t \v \xHH
*For \xHH, HH is any 2 hexadecimal characters.
```

A string declared with double quotes doesn't need to use \\' and vice versa.

Finally, in spirit with Lua, SWScript also has a weird way to concat strings together. Instead using `+` like any normal language or the `..` of Lua, you get to use `$` instead!

### Variables
A variable name can contain any letters, digits, and underscores. They can't start with digits. SWScript is case sensitive, meaning `x` and `X` are treated as two different variables.

## Operators
### Regular Operators
SWScript has the following operators for use in expressions:
```
Arithmetic:
    +  Addition
    -  Subtraction / Minus
    *  Multiplication
    /  Division
    // Floor Division
    ** Exponentiation
    #  Length (of strings and tables)
    %  Modulo

Boolean:
    && And
    || Or
    !  Not

Bitwise:
    &  And
    |  Or
    ^  Xor
    ~  Not
    << Shift Left
    >> Shift Right

Comparison:
    == Equal
    != Not Equal
    >  Greater Than
    <  Less Than
    >= Greater Than or Equal To
    <= Less Than or Equal To

Misc:
    ++ Increment (Both prefix and postfix)
    -- Decrement (Both prefix and postfix)
    $  String Concatenation
```

### Assignment Operators
In addition to regular operators, there are many operators for assigning values to variables:
```
Misc:
    =  Assign
    ++ Increment (Both prefix and postfix)
    -- Decrement (Both prefix and postfix)
    $= String Concatenation Assign

Arithmetic:
    += Add Assign
    -= Sub Assign
    *= Mul Assign
    /= Div Assign
    //= Floor Div Assign
    **= Pow Assign
    %= Mod Assign

Boolean:
    &&= Bool And Assign
    ||= Bool Or Assign

Bitwise:
    &=  And Assign
    |=  Or Assign
    ^=  Xor Assign
    <<= Shift Left Assign
    >>= Shift Right Assign
```

Also, here's a precedence table for those interested:
```
||
&&
|
^
&
== !=
> < >= <=
<< >>
+ - $
* / // %
- ! ~ # ++ -- (Unary Operators)
**
```

### Short Circuit Evaluation
Short Circuit Evaluation is present for `&&` and `||`, and can be used as a substitute for a ternary operator.

### Truthy / Falsy Values
Like Lua, any value other than false and null is "truthy", being equivalent in if statements etc to a boolean true.

## Statements
### Multi-Variable Assignment
Multiple variables and expressions can be used in one assignment. For the most part this works like Lua, but gets complicated with the extra assignment operators and function calls returning multiple values.
```
a, b = 10, 20       ;a=10 b=20

a, b = b, a         ;a=b b=a

local a, b, c, d    ;local a=null local b=null local c=null local d=null

a, b += 10, 20      ;a+=10 b+=20

func SomeFunctionCall() {return 10, 20}
a, b = SomeFunctionCall() ;a=10 b=20

; mixing a function call with multiple returns and another value allows only one return value.
a, b, c = 5, SomeFunctionCall() ; a=5 b=10 c=null

```

### If Statement
```
if <expression> {

} else if <expression> {

} else {

}
```

### Switch Statement
```
switch <expression> {
    case "abc" { ;must be literal value (string, number, bool) and not null.

    }
    case 123 {

    }
    default { ;runs if no case matches the expression, null runs here too.

    }
}
```

### For Loop
A for loop has two types, one specifying how to modify the iterator each iteration and the other incrementing by 1 by default:
```
;counts by 1's
for i=0, i<10 {
    println(i)
}

;counts by 2's
for i=0, i<10, i+=2 {
    println(i)
}
```

### While Loop
```
while <expression> {

}
```

### Loop Control
`break` stops the loop executing and jumps to the code after it.
`continue` skips to the next iteration of the loop.

### Functions
A function is declared with the keyword `func`, optionally a name, open parenthesis, the parameters seperated by commas, closed parathesis, and a set of braces containing the function body. A function will typically look like this:
```
func factorial(n) {
    local output = 1
    for i=2, i<=n {
        output *= i
    }
    return output
}
```

Functions are first-class in SWScript, meaning they are treated as any other value type and can be assigned, passed to other functions, used in tables, etc. A function without the name is used when it's declared as part of a larger statement, for example in a table.

```
local math = {
    pow = func(a, b) { return a**b }
}

println(math.pow(2, 4)) ;16
```

A SWScript function declared with a name is equivalent to assigning a function to a global variable, and can be called before it is declared:
```
thing() ;this works

func thing() {
    println("thing() ran!")
}
```

### Return
Returns should work as expected.
```
func thing() {
    return 10, 20
}

a, b = thing() ; a=10 b=20
```

In the case that there are less returns than variables being assigned too, the extras will be set to null.
```
func thing() {
    return 10
}

a, b = thing() ; a=10 b=null
```

### @property

Used to define property text/number/boolean (equivalent to those in Stormworks) and assign them values. These are included in the Stormworks Export, LifeboatAPI Export, and loaded into the SW Lua simulator.

The format is `@property <type>, <name>, <value>`. An example for each type is shown below:

```
; type can be 'text', 'number', or 'bool'
; name and value must be string literals.

@property text, "test1", "abc"
@property number, "test2", "123"
@property bool, "test3", "true"
```

### import
Although a reserved keyword, it currently isn't used for anything.

## Tables
A SWScript table is equivalent to a Lua table and are regular Lua tables internally, so the library functions should work on them for the most part.

To declare a table, use a pair of braces:

```
local table = {}
```

A table can also be declared with elements:

```
local table = {123, 456}

println(table[1]) ;123
println(table[2]) ;456
```

Finally, indices can also be specified:

```
local table = {abc = 123, [3] = 456, 789}

println(table.abc) ;123
println(table[3]) ;456
println(table[1]) ;789
```

``.abc`` is a shortcut to reading an index and is equivalent to using ``["abc"]``.

The length of a table can be obtained with the ``#`` operator.

```
local table = {123, 456, 789}

println(#table) ;3
```

## Function Calls
### Self Call
A self-call is usually used to emulate object-oriented programming, and is a feature in Lua that was carried over to SWScript. When calling a function in a table, using `:` instead of `.` to index it will include the table it is called from as the first argument. I call it a self-call because self is usually used as the name of the first parameter.

```
local table = {
    data = 123,
    add = func(self, value) {
        self.data += value
    }
}

println(table.data)   ; 123
table:add(10)       ; same as table.add(table, 10), but cleaner.
println(table.data)   ; 133
```

### Calling Lua Functions
A Lua function is called using the `lua` keyword as shown below:

```
func onTick() {
    local number = lua.input.getNumber(1)
}
```

This can be shortened by assigning lua.x.y.z to another variable once elsewhere in the program. This is also better for performance:
```
gn = lua.input.getNumber
func onTick() {
    local number = gn(1)
}
```

Although `lua` uses _ENV in the background it is much more limited and can only be used to access functions. `lua.math.pi` or `input=lua.input` does not work.

## Built-In Functions
### print / println
When running in the Stormworks VM, these are equivalent and pass the parameter to debug.log (the output of which can be seen with an external program such as [DebugView](https://learn.microsoft.com/en-us/sysinternals/downloads/debugview)). In the built-in VM println adds a new-line after the value printed to console, while print doesn't.

### _keys()
This function takes a table variable as input and returns a table indexed from 1 containing the indices of the table. This is meant to make an equivalent to Lua's `for k, v in pairs(thing) do end` possible, although a bit more bulky. Otherwise there would be no way to iterate though a table which you don't know the indices of.

### _type()
Equivalent to Lua's `type()`, and returns the type of a variable passed to it as a string. One important note is that it runs Lua's type on it internally, and SWScript functions passed to it will return `table` instead of `function`.

# Bytecode & VM
## Instruction Set
SWScript uses a stack based bytecode vm and is inspired by Lua 4.0 (which was a stack machine, while Lua 5.0 and onwards used a register machine). Below is the full instruction set, they are grouped by similar instructions while the number to the right is the actual opcode. The opcodes are in a different order to group instructions based on their stack operations. For example those with 1 argument, 2 arguments, a value pushed back to the stack, etc.

The thought process behind this many instructions was to gain some speed by needing to run less instructions overall, using more specialized ones instead of a bunch of simple ones. For example, there are many conditional jump instructions so a combination doesn't need to be used to evaluate an expression, and instructions such as op_loadimm which optimize common operations. There are also faster versions of instructions, for example op_loadbool could be replaced with the op_loadconst instruction, but it would be slightly slower to run because it needs a table access.
```
    op_getconst = 38
    op_getlocal = 39
    op_getupval = 40
    op_getglobal = 41
    op_setlocal = 22
    op_setupval = 23
    op_setglobal = 24
    op_dup = 25

    op_loadimm = 42
    op_loadbool = 43
    op_loadnull = 44

    op_add = 0
    op_sub = 1
    op_mul = 2
    op_div = 3
    op_floordiv = 4
    op_pow = 5
    op_mod = 6
    op_bitand = 7
    op_bitor = 8
    op_bitxor = 9
    op_bitshiftleft = 10
    op_bitshiftright = 11

    op_addimm = 46
    op_minus = 32
    op_boolnot = 33
    op_bitnot = 34
    op_len = 35

    op_concat = 12

    op_eq = 13
    op_neq = 14
    op_lt = 15
    op_lte = 16

    op_jmp = 47
    op_jindexed = 31
    op_jeq = 18
    op_jneq = 19
    op_jgt = 20
    op_jgte = 21
    op_jfalse = 26
    op_jtrue = 27
    op_jfnp = 28
    op_jtnp = 29

    op_tablenew = 45
    op_tableset = 50
    op_tableget = 17
    op_tablekeys = 36

    op_closure = 48
    op_call = 51
    op_return = 52

    op_halt = 49
    op_print = 30
    op_type = 37
```

### A Bytecode Instruction
A single instruction is 24 bits. 6 are used for the opcode and 18 for data, of which the highest is a sign bit.

## Compiling

I don't think it would be a good idea to have me explain the whole compiler / vm here (look at \SWScript\Compiler\Parser\sws_Parser.cs for proof), but heres a quick example of what the internals look like without describing how it actually works:

To compile a program, it's first tokenized in two passes, which are then directly turned into bytecode by the parser (the normal way to do is to make an AST first and compile that).

This source code:
```
func factorial(n) {
    local output = 1
    for i=2, i<=n {
        output*=i
    }
    return output
}

print(factorial(5))
```

Becomes this:
```
     #   Type                                  Ln / Col: Literal
     0   keyword_func                           1 /   4:
     1   identifier                             1 /  14: factorial
     2   punctuation_parenthesis_open           1 /  15:
     3   identifier                             1 /  16: n
     4   punctuation_parenthesis_closed         1 /  17:
     5   punctuation_braces_open                1 /  19:
     6   keyword_local                          2 /   9:
     7   identifier                             2 /  16: output
     8   operator_assign                        2 /  18:
     9   literal_number                         2 /  20: 1
    10   keyword_for                            3 /   7:
    11   identifier                             3 /   9: i
    12   operator_assign                        3 /  10:
    13   literal_number                         3 /  11: 2
    14   punctuation_comma                      3 /  12:
    15   identifier                             3 /  14: i
    16   operator_lte                           3 /  15:
    17   identifier                             3 /  17: n
    18   punctuation_braces_open                3 /  19:
    19   identifier                             4 /  14: output
    20   operator_mul_assign                    4 /  15:
    21   identifier                             4 /  17: i
    22   punctuation_braces_closed              5 /   5:
    23   keyword_return                         6 /  10:
    24   identifier                             6 /  17: output
    25   punctuation_braces_closed              7 /   1:
    26   keyword_print                          9 /   5:
    27   punctuation_parenthesis_open           9 /   6:
    28   identifier                             9 /  15: factorial
    29   punctuation_parenthesis_open           9 /  16:
    30   literal_number                         9 /  17: 5
    31   punctuation_parenthesis_closed         9 /  18:
    32   punctuation_parenthesis_closed         9 /  19:
    33   EOF                                   -1 /  -1:
```

And then this:
```
func () {
     0  op_closure 0          ;factorial
     1  op_setglobal 0        ;factorial
     2  op_loadimm 5
     3  op_getglobal 0        ;factorial
     4  op_call 257           ;Args:1 Returns:1
     5  op_print 1
     6  op_halt
}

Locals (0)

Upvalues (0)

Closures (1)
     0  factorial

* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
func factorial() {
     0  op_loadimm 1
     1  op_setlocal 1         ;output
     2  op_loadimm 2
     3  op_setlocal 2         ;i
     4  op_getlocal 0         ;n
     5  op_getlocal 2         ;i
     6  op_jgt 8              ;Jump To:15
     7  op_getlocal 2         ;i
     8  op_getlocal 1         ;output
     9  op_mul
    10  op_setlocal 1         ;output
    11  op_getlocal 2         ;i
    12  op_addimm 1
    13  op_setlocal 2         ;i
    14  op_jmp -11            ;Jump To:4
    15  op_getlocal 1         ;output
    16  op_return 1
}

Locals (3)
     0  n  0, 17, 1
     1  output  2, 17, 1
     2  i  3, 15, 2

Upvalues (0)

Closures (0)

**** Globals (1) ****
     0  factorial

**** Constants (1) ****
     0  factorial  (String)
```