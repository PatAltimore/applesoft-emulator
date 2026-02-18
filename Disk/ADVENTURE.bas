10 HOME
20 PRINT "WELCOME TO THE MYSTERY CAVE"
30 PRINT ""
40 PRINT "You awaken in a dark cave with no memory"
50 PRINT "of how you got there."
60 PRINT ""
70 PRINT "You see three paths:"
80 PRINT "1. Left - A faint glow of light"
90 PRINT "2. Center - A strange humming sound"
100 PRINT "3. Right - A cool breeze"
110 PRINT ""
120 PRINT "Which path do you choose? (1, 2, or 3)"
130 INPUT C
140 IF C = 1 THEN GOTO 200
150 IF C = 2 THEN GOTO 300
160 IF C = 3 THEN GOTO 500
170 PRINT "Invalid choice! Try again."
180 GOTO 120
190 REM === LEFT PATH ===
200 HOME
210 PRINT "You follow the light..."
220 PRINT ""
230 PRINT "After a short walk, you emerge into a"
240 PRINT "beautiful crystal cavern filled with"
250 PRINT "glowing stones. You found an exit!"
260 PRINT ""
270 PRINT "You are FREE!"
280 PRINT ""
290 GOTO 700
295 REM === CENTER PATH ===
300 HOME
310 PRINT "You approach the humming sound..."
320 PRINT ""
330 PRINT "You discover an ancient stone door with"
340 PRINT "a puzzle inscribed on it:"
350 PRINT ""
360 PRINT "What is 3 + 5?"
370 INPUT A
380 IF A = 8 THEN GOTO 430
390 PRINT "WRONG! The door locks permanently."
400 PRINT "You are trapped forever..."
410 PRINT ""
420 GOTO 700
425 REM === CENTER PATH - CORRECT ANSWER ===
430 PRINT "CORRECT! The door opens with a rumble."
440 PRINT ""
450 PRINT "You step through and find yourself in a"
460 PRINT "treasure chamber filled with gold and"
470 PRINT "precious jewels!"
480 PRINT ""
490 PRINT "You are RICH and FREE!"
495 PRINT ""
497 GOTO 700
498 REM === RIGHT PATH ===
500 HOME
510 PRINT "You feel the cool breeze..."
520 PRINT ""
530 PRINT "Walking toward the breeze, you find"
540 PRINT "yourself in a narrow passage. It opens"
550 PRINT "into an underground river."
560 PRINT ""
570 PRINT "You find a boat and paddle downstream."
580 PRINT "After hours of travel, you emerge at"
590 PRINT "the mouth of the cave!"
600 PRINT ""
610 PRINT "You are FREE!"
620 PRINT ""
630 GOTO 700
695 REM === END GAME ===
700 PRINT "THE END"
710 PRINT ""
720 PRINT "Play again? (Y/N)"
730 INPUT R$
740 IF R$ = "Y" THEN GOTO 10
750 IF R$ = "N" THEN GOTO 780
760 PRINT "Please enter Y or N."
770 GOTO 730
780 PRINT "Thanks for playing!"
790 END
