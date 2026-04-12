# Applesoft BASIC Emulator

A browser-based Applesoft BASIC emulator for Apple ][ style programs. The entire interpreter runs in your web browser using JavaScript—no backend server required. Programs are saved to your browser's IndexedDB storage.

```
                APPLESOFT BASIC EMULATOR
          Based on Apple ][ Applesoft BASIC

]10 PRINT "HELLO, WORLD!"
]RUN
HELLO, WORLD!

]
```

## Getting Started

### Online (Recommended)

Open the live emulator at: **[ashy-wave-08690e81e.2.azurestaticapps.net](https://ashy-wave-08690e81e.2.azurestaticapps.net)**

Your programs are saved automatically to your browser's local storage.

### Local Development

To run the emulator locally:

```bash
cd web
python -m http.server 5500
```

Then open `http://localhost:5500` in your browser.

The interpreter runs entirely in the browser—no backend API is required.

## Browser-Native Architecture

- **Interpreter Engine:** Full Applesoft interpreter implemented in JavaScript (web/runtime/local-emulator.js)
- **Persistence:** Programs saved to IndexedDB with automatic seeding of bundled programs
- **No Backend Required:** Static web app hosted on Azure Static Web Apps; zero server-side execution
- **Interactive I/O:** INPUT and GET statements pause execution and resume on user submit without restarting
- **Offline Ready:** Bundled programs available offline; SAVE/LOAD work entirely in-browser

## Storage & Data

### Program Storage

Programs are saved to your browser's IndexedDB under the key `programs` with the program name as the index. IndexedDB provides unlimited local storage (up to your browser's quota, typically 50GB per site).

### Clearing Local Data

To reset all programs and variables, clear your browser's site data:
1. Press F12 to open Developer Tools
2. Go to **Application** → **Storage**
3. Click **Clear site data**

Or use the in-emulator `NEW` command to clear the current program and variables (but keep saved programs intact).

## Command Reference

### Direct-Mode Commands

Type these commands at the prompt (preceded by `]`) to control the emulator:

| Command           | Description                          |
|-------------------|--------------------------------------|
| `RUN`             | Run the current program              |
| `RUN 100`         | Run starting from line 100           |
| `LIST`            | List the entire program              |
| `LIST 10`         | List line 10                         |
| `LIST 10,50`      | List lines 10 through 50             |
| `NEW`             | Clear the program and all variables  |
| `SAVE "MYPROG"`   | Save the program to browser storage  |
| `LOAD "MYPROG"`   | Load a program from browser storage  |
| `CATALOG`         | List all saved programs              |
| `DEL 10,50`       | Delete lines 10 through 50           |
| `HELP`            | Show this command summary            |
| `QUIT` / `EXIT`   | Close the emulator session           |

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

## Interactive Input/Output

**INPUT:** Pauses execution and prompts for a line of text. Execution resumes after you type your input and press Enter.

```
]10 INPUT "What is your name? ";NAME$
]20 PRINT "Hello, ";NAME$
```

**GET:** Pauses execution and waits for a single keypress. Useful for games like WUMPUS.

```
]10 GET C$
]20 PRINT "You pressed: ";C$
```

Both INPUT and GET work interactively in the browser—you can pause mid-program, provide input, and the program continues without restarting.

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
- **Program** (`Program.cs`) — ASP.NET Core minimal API host with per-session interpreter state

## Limitations

- No graphics modes (LORES/HIRES) — text mode only
- No sound support
- `PEEK`/`POKE` use simulated memory (no real hardware mapping)
- `CALL` accepts but ignores machine-language addresses
- No cassette or disk I/O beyond `SAVE`/`LOAD`/`CATALOG` to the `Disk/` folder

## License

This project is provided as-is for educational and hobbyist use.
