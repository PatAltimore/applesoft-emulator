# Phase 6: Regression Testing Checklist

Last updated: Phase 4 complete, Phase 6 in progress.

**Testing Goal:** Validate browser interpreter has feature parity with C# interpreter for all bundled programs and core use cases.

**Test Environment:** https://ashy-wave-08690e81e.2.azurestaticapps.net/

---

## Test 1: FIBONACCI Program Execution

**Program Location:** `CATALOG` → `FIBONACCI`

**Expected Output:**
```
0 1 1 2 3 5 8 13 21 34 55 89 144 233 377
```

**Steps:**
1. Open emulator
2. Type: `LOAD "FIBONACCI"`
3. Type: `RUN`
4. Verify output matches exactly (space-separated Fibonacci sequence, ends with newline)
5. Type: `LIST` to confirm program loaded correctly

**Test Status:** ___________

---

## Test 2: MULTIPLICATION Program Execution

**Program Location:** `CATALOG` → `MULTIPLICATION`

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

**Steps:**
1. Type: `NEW` (clear previous state)
2. Type: `LOAD "MULTIPLICATION"`
3. Type: `RUN`
4. Verify 10x10 multiplication table outputs correctly

**Test Status:** ___________

---

## Test 3: GUESS Number Game (Interactive)

**Program Location:** `CATALOG` → `GUESS`

**Expected Behavior:**
- Generates random number 1-100
- Prompts "YOUR GUESS"
- Responds with "TOO LOW!" or "TOO HIGH!" until correct
- Shows "YOU GOT IT!" when correct

**Steps:**
1. Type: `NEW`
2. Type: `LOAD "GUESS"`
3. Type: `RUN`
4. Wait for prompt, enter any number (e.g., 50)
5. Follow prompts: enter numbers until you get it right
6. Verify logic branches correctly (too low/high detection works)
7. Verify game ends with success message

**Test Status:** ___________

---

## Test 4: WUMPUS Game (Complex Interactive)

**Program Location:** `CATALOG` → `WUMPUS`

**Expected Behavior:**
- Game initialization with description
- Prompts for movement decisions
- Uses GET for single-character input
- Navigates 4x4 grid avoiding Wumpus
- Detects hazards (breeze, stench, pit)

**Steps:**
1. Type: `NEW`
2. Type: `LOAD "WUMPUS"`
3. Type: `RUN`
4. Read initial game description
5. Respond to prompts:
   - Enter directions (movement)
   - Enter Y/N for actions
   - Enter numbers for arrow firing
6. Verify:
   - Input pauses execution correctly without rerunning RUN
   - Game state persists across multiple inputs
   - Game detects win/loss conditions

**Known Complexity:** WUMPUS uses nested loops, IF/THEN branching, GET/INPUT, GOSUB/RETURN, variables, and user interaction.

**Test Status:** ___________

---

## Test 5: ADVENTURE Game (Complex State Machine)

**Program Location:** `CATALOG` → `ADVENTURE` (if available)

**Expected Behavior:**
- Text-based adventure game
- Parses player commands
- Maintains game state
- Responds with location descriptions

**Steps:**
1. Type: `NEW`
2. Type: `LOAD "ADVENTURE"`
3. Type: `RUN`
4. Follow game prompts and enter commands
5. Verify state persistence and branching

**Test Status:** ___________

---

## Test 6: INPUT/GET Interactive Pause-Resume

**Test Purpose:** Verify INPUT and GET statements pause execution and resume on user submit without rerunning RUN.

**Program:**
```
10 PRINT "BEFORE INPUT"
20 INPUT "ENTER VALUE";X
30 PRINT "AFTER INPUT, VALUE IS ";X
40 PRINT "DONE"
```

**Steps:**
1. Type: `NEW`
2. Enter the program lines above (lines 10-40)
3. Type: `RUN`
4. Program prints "BEFORE INPUT"
5. Program pauses at INPUT prompt
6. Enter a value (e.g., 42)
7. Verify program continues to line 30
8. Verify output shows "AFTER INPUT, VALUE IS 42"
9. Verify program ends normally

**Key Check:** Program should not restart from line 10 after INPUT; it should resume from line 30.

**Test Status:** ___________

---

## Test 7: GET Single-Character Input

**Test Purpose:** Verify GET pauses for single keypresses.

**Program:**
```
10 PRINT "PRESS A KEY"
20 GET K$
30 PRINT "YOU PRESSED ";K$
```

**Steps:**
1. Type: `NEW`
2. Enter the program lines above
3. Type: `RUN`
4. Program pauses at GET
5. Press any key (e.g., 'A')
6. Verify program resumes and prints "YOU PRESSED A"

**Test Status:** ___________

---

## Test 8: Reset During Tight Loop

**Test Purpose:** Verify RESET button breaks out of infinite loops.

**Program:**
```
10 PRINT "LOOP"
20 GOTO 10
```

**Steps:**
1. Type: `NEW`
2. Enter lines 10-20 above
3. Type: `RUN`
4. Program starts looping, printing "LOOP" repeatedly
5. Verify output is visible and not frozen
6. Click RESET button (top-right corner of terminal)
7. Verify loop stops immediately
8. Verify prompt returns (`]`)
9. Verify terminal is responsive to new commands

**Critical Check:** UI should not freeze; reset should respond within 1-2 seconds at most.

**Test Status:** ___________

---

## Test 9: SAVE/LOAD Persistence

**Test Purpose:** Verify programs are saved to IndexedDB and survive page reload.

**Steps:**
1. Type: `NEW`
2. Enter a simple test program:
   ```
   10 PRINT "MY TEST PROGRAM"
   20 END
   ```
3. Type: `SAVE "TESTPROG"`
4. Type: `CATALOG`
5. Verify "TESTPROG" appears in catalog
6. Close the browser tab completely
7. Reopen the URL: https://ashy-wave-08690e81e.2.azurestaticapps.net/
8. Type: `CATALOG`
9. Verify "TESTPROG" still appears
10. Type: `LOAD "TESTPROG"`
11. Type: `LIST`
12. Verify program is intact

**Test Status:** ___________

---

## Test 10: Text Render Modes (INVERSE/FLASH)

**Test Purpose:** Verify NORMAL/INVERSE/FLASH render correctly.

**Program:**
```
10 INVERSE
20 PRINT "INVERTED TEXT"
30 NORMAL
40 PRINT "NORMAL TEXT"
50 FLASH
60 PRINT "FLASHING TEXT"
70 NORMAL
```

**Steps:**
1. Type: `NEW`
2. Enter program lines 10-70
3. Type: `RUN`
4. Verify:
   - Line 2 output appears with inverted video (black text on white background)
   - Line 4 output appears with normal colors
   - Line 6 output appears with a blinking effect (ANSI escape code applied)

**Test Status:** ___________

---

## Test 11: FOR...NEXT Loop with STEP

**Test Purpose:** Verify FOR loops with STEP work correctly.

**Program:**
```
10 FOR I = 1 TO 10 STEP 2
20 PRINT I;
30 NEXT I
```

**Expected Output:** `1 3 5 7 9`

**Steps:**
1. Type: `NEW`
2. Enter program
3. Type: `RUN`
4. Verify output is exactly "1 3 5 7 9"

**Test Status:** ___________

---

## Test 12: Nested FOR Loops

**Test Purpose:** Verify nested FOR...NEXT loops work.

**Program:**
```
10 FOR I = 1 TO 3
20 FOR J = 1 TO 2
30 PRINT I;",";J;" ";
40 NEXT J
50 PRINT
60 NEXT I
```

**Expected Output:**
```
1,1  1,2 
2,1  2,2 
3,1  3,2 
```

**Steps:**
1. Type: `NEW`
2. Enter program
3. Type: `RUN`
4. Verify output format and values

**Test Status:** ___________

---

## Test 13: GOSUB/RETURN

**Test Purpose:** Verify subroutine calls and returns work.

**Program:**
```
10 PRINT "START"
20 GOSUB 100
30 PRINT "END"
40 END
100 PRINT "IN SUBROUTINE"
110 RETURN
```

**Expected Output:**
```
START
IN SUBROUTINE
END
```

**Steps:**
1. Type: `NEW`
2. Enter program
3. Type: `RUN`
4. Verify order of output and return behavior

**Test Status:** ___________

---

## Test 14: String Functions

**Test Purpose:** Verify LEN, LEFT$, RIGHT$, MID$, ASC, CHR$ work.

**Program:**
```
10 A$ = "HELLO"
20 PRINT "LEN:";LEN(A$)
30 PRINT "LEFT(3):";LEFT$(A$,3)
40 PRINT "RIGHT(2):";RIGHT$(A$,2)
50 PRINT "MID(2,3):";MID$(A$,2,3)
```

**Expected Output:**
```
LEN:5
LEFT(3):HEL
RIGHT(2):LO
MID(2,3):ELL
```

**Steps:**
1. Type: `NEW`
2. Enter program
3. Type: `RUN`
4. Verify all string functions return correct substrings

**Test Status:** ___________

---

## Test 15: Numeric Functions

**Test Purpose:** Verify ABS, INT, SQR, RND work.

**Program:**
```
10 PRINT "ABS(-5):";ABS(-5)
20 PRINT "INT(3.7):";INT(3.7)
30 PRINT "SQR(16):";SQR(16)
40 PRINT "RND BETWEEN 0-1"
50 FOR I = 1 TO 3
60 PRINT RND(1)
70 NEXT I
```

**Steps:**
1. Type: `NEW`
2. Enter program
3. Type: `RUN`
4. Verify:
   - ABS returns 5
   - INT returns 3
   - SQR returns 4
   - RND prints three random numbers between 0 and 1

**Test Status:** ___________

---

## Summary

**Total Tests:** 15

**Passed:** _____ / 15

**Failed:** _____ / 15

**Critical Issues Found:**
- ___________________________
- ___________________________

**Minor Issues/Observations:**
- ___________________________
- ___________________________

**Recommendation:**
- [ ] All tests passed → Ready for production
- [ ] Some non-critical tests failed → Document limitations
- [ ] Critical functionality broken → Escalate for fixes

---

## Notes for Test Runner

1. **Clearing Data:** If you need to reset all programs and variables during testing, use Browser DevTools:
   - F12 → Application → Storage → Click "Clear site data"
   - Or use in-emulator `NEW` command (clears current program/variables only)

2. **Output Timing:** Some programs may have slight delays due to cooperative yielding. If an interactive prompt doesn't appear, wait a moment.

3. **Browser Compatibility:** Tested in Chrome/Edge (WebKit). Firefox may have slight differences in ANSI escape code rendering (INVERSE/FLASH).

4. **Performance:** Monitor browser tab performance during tight-loop tests. Should not freeze or lag noticeably.

5. **IndexedDB Fallback:** If IndexedDB fails, browser falls back to in-memory storage (programs not persisted across refresh).

---

**Next Steps After Regression Testing:**
1. Document any discovered bugs or feature gaps
2. Decide if gaps are acceptable for MVP or require fixes
3. If all tests pass, Phase 6 complete and ready for production/long-term support
