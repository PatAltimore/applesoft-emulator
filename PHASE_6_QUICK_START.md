# Phase 6: Regression Testing - Quick Start Guide

**Status:** ✅ Phase 4 Complete | 🟡 Phase 6 Ready for User Testing

**Date:** April 11, 2026  
**Live Emulator:** https://ashy-wave-08690e81e.2.azurestaticapps.net/

---

## What You Need to Do

Test the browser-based Applesoft interpreter to verify it works like the original C# version. This ensures the migration to static web app was successful.

**Time Required:** 45-60 minutes for full test suite (or 5 minutes for quick validation)

---

## Documentation Files

### 1. **[BASELINE_EXPECTED_OUTPUTS.md](./BASELINE_EXPECTED_OUTPUTS.md)** (Detailed Reference)
- **What:** Expected outputs for each bundled program
- **Why:** Use this to verify your test results are correct
- **How:** Compare your actual output against "Expected Output" section
- **Contains:**
  - 15 test cases with sample programs
  - Step-by-step instructions for each test
  - Expected outputs (exact strings to compare)
  - Validation checklist for each test

### 2. **[TEST_EXECUTION_LOG.md](./TEST_EXECUTION_LOG.md)** (Tracking Sheet)
- **What:** Form to record test results as you execute them
- **Why:** Track which tests pass/fail and capture issue details
- **How:** Open in browser or print, fill in checkboxes and notes
- **Contains:**
  - Quick validation tests (5 min)
  - Full test suite (15 tests)
  - Issue tracking section
  - Summary statistics

### 3. **[REGRESSION_TESTING.md](./REGRESSION_TESTING.md)** (Original Test Suite)
- **What:** Comprehensive checklist with every detail
- **Why:** Full reference if you need more context
- **How:** Scroll to whichever test you're running
- **Contains:**
  - All 15 tests with detailed descriptions
  - Risk levels and complexity assessments
  - Verification points for each test

---

## Quick Start (5 minutes)

**For a fast sanity check:**

1. Open: https://ashy-wave-08690e81e.2.azurestaticapps.net/
2. Type: `LOAD "FIBONACCI"`
3. Type: `RUN`
4. Expected output: `0 1 1 2 3 5 8 13 21 34 55 89 144 233 377`
5. If you see that → ✅ Basic functionality works!

**Then test reset:**
6. Type: `NEW`
7. Type: `10 PRINT "X"` then `20 GOTO 10`
8. Type: `RUN`
9. After seeing output, click **RESET** button (top right)
10. Verify: Loop stops, prompt returns in < 1 second

**If both pass → ✅ Core interpreter is working. You can proceed to full test suite or stop here.**

---

## Full Test Suite (45 minutes)

**Detailed instructions:**

1. Open [BASELINE_EXPECTED_OUTPUTS.md](./BASELINE_EXPECTED_OUTPUTS.md)
2. Open [TEST_EXECUTION_LOG.md](./TEST_EXECUTION_LOG.md) in separate window
3. For each test:
   - Follow the "Steps" in BASELINE_EXPECTED_OUTPUTS.md
   - Record result in TEST_EXECUTION_LOG.md
   - Mark ☐ PASS or ☐ FAIL
   - Note any issues in "Issues" field

**Suggested order:**
1. FIBONACCI (simple, instant)
2. MULTIPLICATION (simple nested loop)
3. Text Modes (visual check)
4. GUESS game (interactive, quick)
5. WUMPUS game (complex, longer)
6. ADVENTURE game (most complex)
7. Reset test (responsiveness)
8. Persistence test (IndexedDB)
9. FOR/STEP, Nested FOR
10. GOSUB/RETURN
11. String functions
12. Numeric functions
13. Multi-variable INPUT
14. Type persistence

---

## What to Look For

### ✅ Signs Everything is Working

- All 15 tests pass ✅
- Output matches expected exactly
- Response time < 1 second for commands
- No error messages in browser console (F12)
- Programs save and load correctly
- Reset button stops tight loops
- INVERSE/FLASH text appears visually different

### ⚠️ Small Issues (Acceptable)

- Minor formatting differences (extra spaces)
- FLASH effect looks different than expected (CSS)
- One or two tests fail but core works
- Slight delay in response (< 2 seconds)

### ❌ Critical Issues (Need Fixing)

- Program crashes ("undefined" error)
- FOR/NEXT loops don't work
- INPUT/GET don't pause (ignore input)
- GOTO/GOSUB jump to wrong lines
- SAVE/LOAD don't work
- Browser freezes during tight loop

---

## Troubleshooting

### Program Won't Load
**Error:** `LOAD "FIBONACCI"` returns error

**Try:**
1. Type `CATALOG` - do you see the program listed?
2. If not: Bundled programs didn't deploy (contact support)
3. If yes: Try loading again, might be timing issue

### Browser Console Errors
**Error:** Red text in F12 → Console tab

**What to do:**
1. Screenshot the error
2. Note which test triggered it
3. Include in issue report

### IndexedDB Not Working
**Symptom:** SAVE "PROG" succeeds but after page reload, program is gone

**Check:**
1. F12 → Application → IndexedDB
2. Look for "applesoft-emulator" database
3. If missing: Try "Clear Site Data" and try again
4. If still fails: Fallback to in-memory storage (programs lost on reload)

### Text Mode (INVERSE/FLASH) Doesn't Show
**Symptom:** INVERSE text looks the same as NORMAL

**Try:**
1. Check F12 → Console for CSS errors
2. Verify web/styles.css has `.crt-inverse` and `.crt-flash` rules
3. Try different browser (Chrome vs Firefox may differ on ANSI rendering)

---

## Recording Results

### Format 1: Text (Simplest)

Save as `PHASE_6_RESULTS.txt`:

```
PHASE 6 REGRESSION TEST RESULTS
Date: April 11, 2026
Tester: [Your Name]
Browser: Chrome 120

QUICK VALIDATION:
Test 1 (FIBONACCI): PASS
Test 2 (Reset): PASS

FULL SUITE:
Test 1 (FIBONACCI): PASS - Output matches
Test 2 (MULTIPLICATION): PASS - 10x10 grid correct
Test 3 (GUESS): PASS - Interactive game works
Test 4 (WUMPUS): PASS - Complex game playable
Test 5 (ADVENTURE): FAIL - Game crashes after "GO EAST"
...

SUMMARY:
Passed: 12/15
Failed: 3/15
Critical Issues: 1 (ADVENTURE crash)
```

### Format 2: Filled Test Log

Fill in [TEST_EXECUTION_LOG.md](./TEST_EXECUTION_LOG.md) checkboxes and save as PDF.

### Format 3: GitHub Issue

Create an issue in the repo with title:

```
Phase 6 Testing: Results - [12/15 passed, 3 issues]
```

And include test log in the issue body.

---

## Expected Results

### Best Case (95% ✅)
All 15 tests pass. Try:
```
]LOAD "FIBONACCI"
]RUN
]LOAD "MULTIPLICATION"
]RUN
]LOAD "WUMPUS"
]RUN
[Play WUMPUS game]
```

All should work flawlessly.

### Good Case (12/15 ✅)
12-14 tests pass, 1-3 minor issues. Examples:
- Text mode visual rendering slightly off
- One game has a minor bug
- Performance slightly slower than expected

**Status:** Shippable with known limitations documented.

### Needs Work Case (< 12 ✅)
More than 3 tests failing. Examples:
- FOR/NEXT loops broken
- INPUT/GET not working
- Programs crashing on load

**Status:** Needs code fixes before shipping.

---

## Next Steps After Testing

### ✅ If All Tests Pass
1. Commit results to a new branch: `git checkout -b phase-6-complete`
2. Add results summary to [Phase 6 Test Report](./PHASE_6_TEST_REPORT.md)
3. Merge back: `git checkout main && git merge phase-6-complete`
4. **Project is COMPLETE and ready for production use!** 🎉

### ⚠️ If Some Tests Fail
1. Document all failures in [TEST_EXECUTION_LOG.md](./TEST_EXECUTION_LOG.md)
2. Create GitHub issues for critical problems
3. Code review local-emulator.js to identify root causes
4. Fix issues or update documentation with known limitations
5. Re-test after fixes

### ❌ If Many Tests Fail
1. Review browser console (F12) for JavaScript errors
2. Check network tab for 404s on bundled programs
3. Verify IndexedDB is accessible
4. Debug local-emulator.js with VS Code debugger
5. Consider starting Phase 6 over with fresh browser session

---

## Files You'll Use

📂 **For Testing:**
- Live URL: https://ashy-wave-08690e81e.2.azurestaticapps.net/
- Reference Guide: [BASELINE_EXPECTED_OUTPUTS.md](./BASELINE_EXPECTED_OUTPUTS.md)
- Tracking Sheet: [TEST_EXECUTION_LOG.md](./TEST_EXECUTION_LOG.md)

📂 **For Context:**
- Original Test Suite: [REGRESSION_TESTING.md](./REGRESSION_TESTING.md)
- Code Review: [PHASE_6_TEST_REPORT.md](./PHASE_6_TEST_REPORT.md)
- Project Summary: [PROJECT_COMPLETION_SUMMARY.md](./PROJECT_COMPLETION_SUMMARY.md)

📂 **If Debugging Needed:**
- Browser Interpreter: `web/runtime/local-emulator.js`
- Frontend Controller: `web/app.js`
- Bundled Programs: `web/disk/` (FIBONACCI.bas, GUESS.bas, WUMPUS.bas, etc.)

---

## Success Criteria

**Phase 6 is COMPLETE when:**

- ✅ 12+ out of 15 tests pass
- ✅ No critical runtime crashes
- ✅ Interactive I/O (INPUT/GET) works
- ✅ Reset button stops tight loops
- ✅ SAVE/LOAD persistence works
- ✅ All major bundled programs load and run

**Result:** Browser interpreter has feature parity with C# version. Architecture successfully migrated to static web app. Ready for production use.

---

## Support

**If you get stuck:**

1. **Check browser console:** F12 → Console → Look for red errors
2. **Check network:** F12 → Network → Look for 404 on disk/
3. **Check IndexedDB:** F12 → Application → IndexedDB → See if database exists
4. **Verify deployment:** Visit https://ashy-wave-08690e81e.2.azurestaticapps.net/ - should load terminal UI
5. **Read local-emulator.js:** Search for your failing command to understand logic

**Contact info:** Include:
- Browser (Chrome/Firefox/Safari/Edge)
- OS (Windows/Mac/Linux)
- Exact test that failed
- Expected vs actual output
- Browser console errors (if any)

---

**Ready to test?**

👉 **[Start with Quick Validation](#quick-start-5-minutes) or jump to [Full Test Suite](#full-test-suite-45-minutes)**

Let's verify the Applesoft emulator works in the browser! 🚀
