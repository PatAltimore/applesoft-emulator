# Phase 6: Expected Outputs & Validation Guide

**Purpose:** Reference baseline expected outputs for regression testing when user runs bundled programs through browser emulator.

**How to Use:** Open browser emulator, execute each program, and compare output with "Expected Output" section below.

---

## Test 1: FIBONACCI Program

**File:** `disk/FIBONACCI.bas`

**Program Code:**
```
10 A = 0 : B = 1
20 FOR I = 1 TO 15
30 PRINT A;
40 C = A + B
50 A = B : B = C
60 NEXT I
70 PRINT
```

**Steps:**
```
]LOAD "FIBONACCI"
]RUN
```

**Expected Output:**
```
0 1 1 2 3 5 8 13 21 34 55 89 144 233 377
```

**Validation Checklist:**
- [ ] All 15 Fibonacci numbers present and in correct order
- [ ] Numbers separated by single space (except last)
- [ ] Newline after 377
- [ ] No extra output or error messages

**Notes:**
- Program calculates F(n) where F(0)=0, F(1)=1, F(n)=F(n-1)+F(n-2)
- FOR I = 1 TO 15 means loop runs 15 times (I: 1,2,3...15)
- PRINT A; prints number + space before newline
- Line 70: PRINT outputs final newline to end sequence

---

## Test 2: MULTIPLICATION Program

**File:** `disk/MULTIPLICATION.bas`

**Program Code:**
```
10 FOR I = 1 TO 10
20 FOR J = 1 TO 10
30 PRINT I * J;
40 NEXT J
50 PRINT
60 NEXT I
```

**Steps:**
```
]NEW
]LOAD "MULTIPLICATION"
]RUN
```

**Expected Output:**
```
1 2 3 4 5 6 7 8 9 10
2 4 6 8 10 12 14 16 18 20
3 6 9 12 15 18 21 24 27 30
4 8 12 16 20 24 28 32 36 40
5 10 15 20 25 30 35 40 45 50
6 12 18 24 30 36 42 48 54 60
7 14 21 28 35 42 49 56 63 70
8 16 24 32 40 48 56 64 72 80
9 18 27 36 45 54 63 72 81 90
10 20 30 40 50 60 70 80 90 100
```

**Validation Checklist:**
- [ ] 10 rows (I = 1 to 10)
- [ ] 10 columns per row (J = 1 to 10)
- [ ] Each cell shows I*J value
- [ ] Values separated by space
- [ ] Each row ends with newline
- [ ] Last row is: `10 20 30 40 50 60 70 80 90 100`

**Notes:**
- Nested FOR loops: outer loop I, inner loop J
- Line 30: PRINT I*J; prints product + space
- Line 50: PRINT (no args) outputs newline between rows
- Total output: 10×10 grid of multiplication results

---

## Test 3: GUESS Game (Interactive)

**File:** `disk/GUESS.bas`

**Program Code:**
```
10 N = INT(RND(1) * 100) + 1
20 PRINT "I'M THINKING OF A NUMBER (1-100)"
30 INPUT "YOUR GUESS";G
40 IF G < N THEN PRINT "TOO LOW!" : GOTO 30
50 IF G > N THEN PRINT "TOO HIGH!" : GOTO 30
60 PRINT "YOU GOT IT!"
```

**Steps:**
```
]NEW
]LOAD "GUESS"
]RUN
[Wait for prompt - it will say "YOUR GUESS"]
[Enter guess, e.g., 50]
[Program responds TOO LOW or TOO HIGH]
[Keep guessing until you get it right]
```

**Expected Output Sequence:**
1. Program prints: `I'M THINKING OF A NUMBER (1-100)`
2. Program pauses at: `YOUR GUESS` prompt
3. You enter a number (e.g., 50)
4. Program responds: `TOO LOW!` (if your guess < N) or `TOO HIGH!` (if your guess > N)
5. Program loops back to line 30, prompts again: `YOUR GUESS`
6. Repeat until your guess = N
7. Program prints: `YOU GOT IT!`

**Key Test Points:**
- [ ] INPUT prompt appears and waits for user input (doesn't auto-continue)
- [ ] Logic branches correctly (too low vs too high)
- [ ] Loop continues until correct number guessed
- [ ] Success message appears when correct
- [ ] RND produces different number on each RUN

**Notes:**
- RND(1) produces random decimal 0-1
- INT(RND(1) * 100) converts to integer 0-99
- Add 1 to get range 1-100
- Each run should have different target number

---

## Test 4: WUMPUS Game (Complex Interactive)

**File:** `disk/WUMPUS.bas`

**Complexity Level:** ⭐⭐⭐⭐ (Advanced - Multi-stage Game)

**Game Description:**
- You are in a 4×4 cave grid
- Wumpus (monster) is in one room
- Pits (deadly) are in other rooms
- Arrows can be fired to kill Wumpus
- You navigate the cave trying to avoid hazards and kill Wumpus

**Expected Gameplay Flow:**
1. Program prints welcome/instructions
2. Prints initial location and hazard warnings (breeze, stench, pit)
3. Waits for movement command (direction: arrow keys or N/S/E/W)
4. You move and program updates location
5. Detects hazards or encounters
6. Eventually win (kill Wumpus) or lose (fall in pit or eaten)

**Key Test Points:**
- [ ] GET pauses for single character input
- [ ] Program state persists across moves
- [ ] Hazard detection works (breeze, stench)
- [ ] Arrow firing works
- [ ] Win/lose conditions detected
- [ ] Can play multiple moves without restarting

**Notes:**
- This is the most complex test
- Uses GET (single char) not INPUT
- Heavy use of GOSUB/RETURN
- Array variables for cave state
- If game doesn't work, this is the first regression to suspect

---

## Test 5: ADVENTURE Game

**File:** `disk/ADVENTURE.bas`

**Complexity Level:** ⭐⭐⭐⭐⭐ (Very Advanced - Text Adventure)

**Game Description:**
- Text-based adventure game
- Parser processes player commands (e.g., "GO NORTH", "TAKE LAMP")
- Tracks inventory and game state
- Multiple rooms/scenes
- Win condition when objectives complete

**Expected Gameplay:** 
1. Program prints initial scene/location description
2. Prints available commands or prompt for action
3. You type command (e.g., "GO EAST" or "LOOK")
4. Program updates game state and prints result
5. Continue until reaching end state (win/lose)

**Key Test Points:**
- [ ] Initial scene description prints
- [ ] Command prompt appears
- [ ] Navigation commands work (GO direction)
- [ ] Object interaction works (TAKE item, EXAMINE item)
- [ ] Inventory tracking works
- [ ] Game state persists across commands
- [ ] Win condition triggers when reached

**Notes:**
- This is the largest program
- Complex string parsing and state machine
- May have subtle bugs if expression evaluator has issues
- Good comprehensive validation of interpreter

---

## Test 6: Text Mode Validation (INVERSE/FLASH)

**Program to Type:**
```
]10 INVERSE
]20 PRINT "THIS IS INVERTED"
]30 NORMAL
]40 PRINT "THIS IS NORMAL"
]50 FLASH
]60 PRINT "THIS IS FLASHING"
]70 NORMAL
]RUN
```

**Expected Visual Output:**
1. First line: White/light text on dark background (inverted video)
2. Second line: Normal black text on light background
3. Third line: Text that blinks/flashes (pulse effect)
4. Normal text mode resets after each section

**Validation:**
- [ ] "THIS IS INVERTED" appears with inverted colors (visually distinct)
- [ ] "THIS IS NORMAL" appears in standard text colors
- [ ] "THIS IS FLASHING" appears with blinking animation
- [ ] No error messages
- [ ] Modes switch cleanly between text sections

**Notes:**
- Relies on CSS classes in web/styles.css
- .crt-inverse for inverted video
- .crt-flash for blinking animation
- ANSI escape codes (\u001b[7m, \u001b[5m, \u001b[0m) should map to CSS

---

## Test 7: FOR/NEXT with STEP

**Program to Type:**
```
]10 FOR I = 1 TO 10 STEP 2
]20 PRINT I;
]30 NEXT I
]RUN
```

**Expected Output:**
```
1 3 5 7 9
```

**Validation:**
- [ ] Prints: 1 3 5 7 9
- [ ] Correct step increment (by 2)
- [ ] Stops at 10 (doesn't go past)
- [ ] Space-separated values
- [ ] Newline at end

---

## Test 8: Nested FOR Loops

**Program to Type:**
```
]10 FOR I = 1 TO 3
]20 FOR J = 1 TO 2
]30 PRINT I;",";J;" ";
]40 NEXT J
]50 PRINT
]60 NEXT I
]RUN
```

**Expected Output:**
```
1,1  1,2 
2,1  2,2 
3,1  3,2 
```

**Validation:**
- [ ] 3 rows (outer loop: I = 1 to 3)
- [ ] 2 values per row (inner loop: J = 1 to 2)
- [ ] Format: "I,J " for each cell
- [ ] Newline between rows
- [ ] Last row: "3,1  3,2 "

---

## Test 9: GOSUB/RETURN (Subroutines)

**Program to Type:**
```
]10 PRINT "START"
]20 GOSUB 100
]30 PRINT "END"
]40 END
]100 PRINT "IN SUBROUTINE"
]110 RETURN
]RUN
```

**Expected Output:**
```
START
IN SUBROUTINE
END
```

**Validation:**
- [ ] Prints "START" first
- [ ] Jumps to line 100 (GOSUB)
- [ ] Prints "IN SUBROUTINE"
- [ ] Returns to line 30 (after GOSUB)
- [ ] Prints "END"
- [ ] Exits normally (END statement)

**Notes:**
- GOSUB pushes return address on stack
- RETURN pops stack and continues
- Different from GOTO (GOSUB remembers return point)

---

## Test 10: SAVE/LOAD Persistence

**Steps:**
```
]NEW
]10 PRINT "MY TEST PROGRAM"
]20 END
]SAVE "TESTPROG"
]CATALOG
[Verify TESTPROG appears in list]
[Close browser tab completely]
[Reopen: https://ashy-wave-08690e81e.2.azurestaticapps.net/]
]CATALOG
[Verify TESTPROG still appears]
]LOAD "TESTPROG"
]LIST
[Verify program is intact]
]RUN
```

**Expected Output:**
- [ ] Line 1: SAVE succeeds (no error)
- [ ] CATALOG shows: TESTPROG
- [ ] After page reload, CATALOG still shows TESTPROG
- [ ] LOAD succeeds
- [ ] LIST shows: 10 PRINT "MY TEST PROGRAM" / 20 END
- [ ] RUN prints: MY TEST PROGRAM

**Key Test Point:** IndexedDB persistence - program survives browser restart

---

## Test 11: Reset During Tight Loop

**Steps:**
```
]NEW
]10 PRINT "LOOP"
]20 GOTO 10
]RUN
[Watch output for several seconds - should see LOOP repeated many times]
[Click RESET button (top-right corner)]
[Verify loop stops and prompt returns]
```

**Expected Behavior:**
- [ ] Program starts printing "LOOP" repeatedly
- [ ] Output is visible (not frozen)
- [ ] After RESET clicked, loop stops immediately (< 1 second)
- [ ] Prompt (]) returns
- [ ] Terminal is responsive to new commands

**Key Test Point:** Cooperative stop mechanism - reset breaks infinite loops

---

## Test 12: String Functions

**Program to Type:**
```
]10 A$ = "HELLO"
]20 PRINT "LEN:";LEN(A$)
]30 PRINT "LEFT(3):";LEFT$(A$,3)
]40 PRINT "RIGHT(2):";RIGHT$(A$,2)
]50 PRINT "MID(2,3):";MID$(A$,2,3)
]RUN
```

**Expected Output:**
```
LEN:5
LEFT(3):HEL
RIGHT(2):LO
MID(2,3):ELL
```

**Validation:**
- [ ] LEN returns 5 (length of "HELLO")
- [ ] LEFT$ returns "HEL" (first 3 chars)
- [ ] RIGHT$ returns "LO" (last 2 chars)
- [ ] MID$ returns "ELL" (starting at pos 2, length 3)

---

## Test 13: Numeric Functions

**Program to Type:**
```
]10 PRINT "ABS(-5):";ABS(-5)
]20 PRINT "INT(3.7):";INT(3.7)
]30 PRINT "SQR(16):";SQR(16)
]RUN
```

**Expected Output:**
```
ABS(-5):5
INT(3.7):3
SQR(16):4
```

**Validation:**
- [ ] ABS(-5) returns 5 (absolute value)
- [ ] INT(3.7) returns 3 (integer part)
- [ ] SQR(16) returns 4 (square root)

---

## Test 14: INPUT Multiple Variables

**Program to Type:**
```
]10 INPUT "ENTER TWO NUMBERS";A,B
]20 PRINT "SUM:";A+B;" PRODUCT:";A*B
]RUN
```

**Expected Flow:**
1. Program prints: `ENTER TWO NUMBERS`
2. Program pauses for input
3. You enter: `5,10` (or separated by space or comma)
4. Program calculates and prints: `SUM:15 PRODUCT:50`

**Validation:**
- [ ] INPUT prompt appears
- [ ] Accepts two values
- [ ] Calculation works
- [ ] Output shows correct sum (15) and product (50)

---

## Test 15: Variable Type Persistence

**Program to Type:**
```
]10 N = 42
]20 S$ = "HELLO"
]30 PRINT N;",";S$
]40 SAVE "TYPEPROG"
]LOAD "TYPEPROG"
]RUN
```

**Expected Output:**
```
42,HELLO
```

**Validation:**
- [ ] Numeric variable N persists as number (42)
- [ ] String variable S$ persists as string ("HELLO")
- [ ] After LOAD, types are preserved
- [ ] Output matches original

**Key Test Point:** Type information survives SAVE/LOAD

---

## Summary Validation Table

| Test # | Program | Expected | Pass | Notes |
|--------|---------|----------|------|-------|
| 1 | FIBONACCI | 0 1 1 2 3 5 8 13 21 34 55 89 144 233 377 | ☐ | Order & spacing |
| 2 | MULTIPLICATION | 10×10 table | ☐ | Grid format |
| 3 | GUESS | Interactive game | ☐ | INPUT pause |
| 4 | WUMPUS | Complex game | ☐ | GET prompt |
| 5 | ADVENTURE | Text adventure | ☐ | State machine |
| 6 | Text Modes | INVERSE/FLASH | ☐ | Visual appearance |
| 7 | STEP | 1 3 5 7 9 | ☐ | Loop increment |
| 8 | Nested | 3×2 grid | ☐ | Loop stack |
| 9 | GOSUB | START / IN SUBROUTINE / END | ☐ | Return address |
| 10 | Persistence | SAVE/LOAD survives reload | ☐ | IndexedDB |
| 11 | Reset | Loop stops < 1s | ☐ | Cooperative stop |
| 12 | Strings | LEN/LEFT/RIGHT/MID | ☐ | Function results |
| 13 | Numerics | ABS/INT/SQR | ☐ | Math functions |
| 14 | Multi INPUT | Handles A,B | ☐ | Variable parsing |
| 15 | Types | Numbers & strings | ☐ | Type persistence |

---

## Issue Reporting

If any test fails:

1. **Capture Error Detail:**
   - Exact error message (if any)
   - Actual output vs. expected output
   - Which line/statement failed
   - Browser console errors (F12 → Console)

2. **Example Issue Report:**
   ```
   Test 2 (MULTIPLICATION): FAILED
   Expected: 10×10 table with last row "10 20 30 40 50 60 70 80 90 100"
   Actual: Only printed first 5 rows then quit
   Error: "ReferenceError: NEXT undefined" in browser console
   ```

3. **Escalation:**
   - If > 50% of tests fail → Critical runtime issue
   - If 20-50% fail → Feature compatibility gaps
   - If < 20% fail → Edge case bugs

---

## Test Execution Notes

**Browser Location:** https://ashy-wave-08690e81e.2.azurestaticapps.net/

**Duration:** ~45 minutes for full test suite

**Order:** 
1. Simple tests first (FIBONACCI, MULTIPLICATION)
2. Interactive tests (GUESS, WUMPUS, ADVENTURE)
3. Feature tests (Reset, Persistence, Text Modes)
4. Edge cases (Nested loops, GOSUB, Functions)

**Success Criteria:**
- ✅ 12-15 tests pass → Ready for production
- ⚠️ 8-11 tests pass → Acceptable with known limitations documented
- ❌ < 8 tests pass → Blocker issues need fixing

---

**Next Step:** Open https://ashy-wave-08690e81e.2.azurestaticapps.net/ and begin executing tests in order. Mark checkbox (☐ → ✓) as each test completes.
