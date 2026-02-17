# Applesoft BASIC Emulator

A Windows command-line emulator for Apple II Applesoft BASIC, written in C#. Create, edit, and run simple Applesoft BASIC programs just like on a real Apple ][.

```
                APPLESOFT BASIC EMULATOR
          Based on Apple ][ Applesoft BASIC

]10 PRINT "HELLO, WORLD!"
]RUN
HELLO, WORLD!

]
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Build & Run

```bash
dotnet build
dotnet run
```

The emulator launches with the classic `]` prompt. Type BASIC commands directly or enter line-numbered programs. Type `QUIT` or `EXIT` to leave.

## Usage

### Entering a Program

Type lines with line numbers to store them. Lines are automatically sorted by number.

```
]10 PRINT "HELLO, WORLD!"
]20 FOR I = 1 TO 5
]30 PRINT "COUNT: ";I
]40 NEXT I
```

### Editing a Program

- **Replace a line:** Re-enter it with the same line number
- **Delete a line:** Type just the line number with nothing after it
- **Delete a range:** `DEL 20,40`

### Direct-Mode Commands

| Command           | Description                          |
|-------------------|--------------------------------------|
| `RUN`             | Run the current program              |
| `RUN 100`         | Run starting from line 100           |
| `LIST`            | List the entire program              |
| `LIST 10`         | List line 10                         |
| `LIST 10,50`      | List lines 10 through 50             |
| `NEW`             | Clear the program and all variables  |
| `SAVE "file.bas"` | Save the program to a file           |
| `LOAD "file.bas"` | Load a program from a file           |
| `DEL 10,50`       | Delete lines 10 through 50           |
| `QUIT` / `EXIT`   | Exit the emulator                    |

## Supported BASIC Statements

| Statement               | Example                                    |
|-------------------------|--------------------------------------------|
| `PRINT`                 | `PRINT "HELLO"` or `? "HELLO"`             |
| `INPUT`                 | `INPUT "NAME? ";N$`                        |
| `LET` (optional)        | `LET X = 10` or `X = 10`                  |
| `IF...THEN`             | `IF X > 5 THEN PRINT "BIG"`               |
| `IF...THEN` (line #)    | `IF X = 0 THEN 100`                       |
| `GOTO`                  | `GOTO 200`                                 |
| `GOSUB` / `RETURN`      | `GOSUB 1000`                               |
| `FOR...TO...STEP`       | `FOR I = 1 TO 10 STEP 2`                  |
| `NEXT`                  | `NEXT I`                                   |
| `DIM`                   | `DIM A(20), B$(10)`                        |
| `DATA` / `READ`         | `DATA 1,2,3` / `READ X`                   |
| `RESTORE`               | Reset DATA pointer to beginning            |
| `DEF FN`                | `DEF FN SQ(X) = X * X`                    |
| `ON...GOTO` / `GOSUB`   | `ON X GOTO 100,200,300`                   |
| `REM`                   | `REM THIS IS A COMMENT`                   |
| `HOME`                  | Clear the screen                           |
| `HTAB` / `VTAB`         | `HTAB 10` / `VTAB 5`                      |
| `POKE`                  | `POKE 768,0`                               |
| `END` / `STOP`          | End program execution                      |

### Multiple Statements Per Line

Separate statements with a colon:

```
]10 X = 5 : Y = 10 : PRINT X + Y
```

## Operators

| Type        | Operators                        |
|-------------|----------------------------------|
| Arithmetic  | `+`  `-`  `*`  `/`  `^`         |
| Comparison  | `=`  `<>`  `<`  `>`  `<=`  `>=` |
| Logical     | `AND`  `OR`  `NOT`               |
| String      | `+` (concatenation)              |

## Built-in Functions

### Numeric

| Function  | Description                |
|-----------|----------------------------|
| `ABS(X)`  | Absolute value             |
| `INT(X)`  | Floor (integer part)       |
| `SQR(X)`  | Square root                |
| `RND(X)`  | Random number (0 to 1)     |
| `SGN(X)`  | Sign (-1, 0, or 1)        |
| `SIN(X)`  | Sine                       |
| `COS(X)`  | Cosine                     |
| `TAN(X)`  | Tangent                    |
| `ATN(X)`  | Arctangent                 |
| `LOG(X)`  | Natural logarithm          |
| `EXP(X)`  | e raised to power X        |
| `PEEK(X)` | Read simulated memory      |
| `POS(X)`  | Current cursor column      |

### String

| Function          | Description                              |
|-------------------|------------------------------------------|
| `LEN(A$)`         | Length of string                          |
| `LEFT$(A$,N)`     | First N characters                        |
| `RIGHT$(A$,N)`    | Last N characters                         |
| `MID$(A$,S,N)`    | Substring from position S, length N       |
| `STR$(X)`         | Convert number to string                  |
| `VAL(A$)`         | Convert string to number                  |
| `CHR$(X)`         | Character from ASCII code                 |
| `ASC(A$)`         | ASCII code of first character             |

## Variables

- **Numeric:** `X`, `A1`, `SCORE` — default value is `0`
- **String:** `N$`, `NAME$` — default value is `""`
- **Arrays:** `DIM A(10)` creates indices 0-10; auto-dimensions to 0-10 if used without DIM

## Example Programs

### Fibonacci Sequence

```
10 A = 0 : B = 1
20 FOR I = 1 TO 15
30 PRINT A;
40 C = A + B
50 A = B : B = C
60 NEXT I
70 PRINT
```

### Guess the Number

```
10 N = INT(RND(1) * 100) + 1
20 PRINT "I'M THINKING OF A NUMBER (1-100)"
30 INPUT "YOUR GUESS";G
40 IF G < N THEN PRINT "TOO LOW!" : GOTO 30
50 IF G > N THEN PRINT "TOO HIGH!" : GOTO 30
60 PRINT "YOU GOT IT!"
```

### Multiplication Table

```
10 FOR I = 1 TO 10
20 FOR J = 1 TO 10
30 PRINT I * J;
40 NEXT J
50 PRINT
60 NEXT I
```

## Architecture

The emulator is composed of three main components:

- **Tokenizer** (`Tokenizer.cs`) — Lexes input into tokens (keywords, numbers, strings, operators)
- **Expression Evaluator** (`ExpressionEvaluator.cs`) — Recursive-descent parser handling operator precedence, function calls, and variable access
- **Interpreter** (`Interpreter.cs`) — Manages program storage, executes statements, and handles flow control (GOTO, GOSUB, FOR/NEXT, IF/THEN)
- **Program** (`Program.cs`) — REPL loop with the classic `]` prompt

## Limitations

- No graphics modes (LORES/HIRES) — text mode only
- No sound support
- `PEEK`/`POKE` use simulated memory (no real hardware mapping)
- `CALL` accepts but ignores machine-language addresses
- No cassette or disk I/O beyond `SAVE`/`LOAD` to local files

## License

This project is provided as-is for educational and hobbyist use.
