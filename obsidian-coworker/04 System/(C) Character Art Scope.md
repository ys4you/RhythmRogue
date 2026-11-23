# (C) Character Art Scope

Decision from the directional-animation research, July 2, 2026. Per-lane sing poses are charm, not function: the arrows and receptors carry all the timing, the character only reacts. So invest art by screen-time, not by assuming every character needs four hand-drawn directions.

## Tiers

- **T1 full (idle + 4 sing + miss):** player character and signature/act bosses only. The ones players stare at and screenshot.
- **T2 reduced (idle + 2 poses, mirror left/right with flipX):** recurring mid enemies. Reads as fully animated at speed for half the art.
- **T3 single-sprite transform pose (current default):** minor and common enemies, prototypes, anything unvalidated. Lean and pop one sprite. Already shipping this; it is a legit approach, not a stopgap.
- **T0 palette swap:** elite and variant reskins of T1/T2 art, near free.

## Rules

- Ship the whole game on T3 placeholder posing first. Do not hand-draw a final directional set until the loop is validated as fun.
- Invest in order of screen-time: player, then first boss, then recurring enemies. Stop when it stops paying off.
- Timing over frame count. Make the bop and pose snap to the beat before adding frames. Two to four frame idles are plenty (Celeste's run is four frames).
- Aseprite: one file per character, tag the ranges (idle, singLEFT, and so on), Split-Tags export to PNG + JSON so a T3 to T1 upgrade later is a drop-in, not a rebuild.

Full research with sources is in the chat if the evidence is ever needed.
