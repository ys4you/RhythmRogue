# (C) Known Bugs and Playtest Plan

> What playtesters found, what was done about it, and the plan for the next round. Updated November 23, 2026.

## Playtests so far

| Date | Tester | Format | How far | Report |
|---|---|---|---|---|
| June 6, 2026 | Playtester 1 (sometimes plays rhythm games) | In person | Fought every enemy including the High Priest (result not recorded) | [[02 Playtesting/2026-06-06_playtester-1\|Playtester 1]] |
| June 13, 2026 | Wendy (sometimes plays rhythm games and roguelikes) | Solo, remote form | Stuck on the 2nd enemy after about 17 minutes of retries | [[02 Playtesting/2026-06-13_wendy\|Wendy]] |

Session logs from the fixes in between: [[07 Iteration Logs/Dev Logs/2026-06-08_session-log|Dev log June 8]] (response to Playtest 1).

**GDD success criterion:** the prototype is validated when **at least 3 external playtesters complete a full run without blocking bugs.** Current count: 0 confirmed. Do not move this goalpost mid-test.

## Findings and what happened to them

| Finding | Seen by | Response | Verified? |
|---|---|---|---|
| No damage after dying and restarting (blocking) | P1 | Fixed June 7 (`HealthComponent` death latch cleared on reset) | Yes. Wendy retried many times and kept taking damage |
| Opening notes pop in instead of scrolling | P1 | Fixed June 8 (music starts immediately, note-free lead-in) | Yes. Wendy: "easy to follow" |
| Misses register a beat late | P1 | Fixed June 8 (misses register at the hit line) | Yes. Wendy: misses felt fair |
| Font hard to read | P1 | Unknown if changed | Wendy had no problem |
| **Difficulty: flat start, then a wall** (Cultist felt like Acolyte, Zealot and High Priest too fast) | P1, Wendy | `DifficultyCurve` with area band, depth and run tier (June 14), NPS-based chart density (June 16), inverted band fixed (June 17). Counter-Attack on the Cultist (Sept 25) | **No, untested** |
| **Enemy side is ignored** ("he just exists", Wendy thought the right side was her) | P1, Wendy | Guard mechanic (June 8), guard recovery (June 16), onboarding coach-mark on the guard badge (June 20), Counter-Attack (Sept 25) | **No.** Wendy (June 13) still ignored it, the rest is untested |
| **Relics not understood** | P1, Wendy | Onboarding relic lesson with coach-mark (June 20) | **No, untested** |
| Boss feels like an NPC standing there (P1 wants it alive, "Gambonanza x Undertale") | P1 | Character animation (July 2), Last Stand revive with announcer and chart escalation (Sept 22) | No, untested |
| No feedback when a hold note is hit late | P1 | No commit addresses it | No |
| Shop hard to find (Wendy's #1 change request) | Wendy | No commit addresses it. Partly because she never got far enough to reach one | No |
| Didn't realise a map path can't be undone | Wendy | No commit addresses it | No |
| Rest didn't heal to full (frustrating) | P1 | Design choice, unchanged | n/a |

**Known blocking bugs right now:** none on record. The game hasn't been played since June, so treat that as unknown until you play it.

## Next round

**Target date:** Sunday December 13, 2026 (proposed).

1. **Self-playtest first.** You've been away four months, so you're the closest thing to a fresh player you'll get. Play 3 full runs end to end, note the seed each time, write down everything that feels off without fixing anything, then fix only blockers and the most repeated complaints.
2. **Then 2 to 3 external testers**, using [[02 Playtesting/Templates/_TEMPLATE_SOLO|the solo form]] (or [[02 Playtesting/Templates/_TEMPLATE|the in-person form]] if you can watch). Wendy is worth asking again: she can tell you directly whether the wall at enemy 2 is gone.
3. **What this round needs to answer:**
   - Can a sometimes-player get past node 2 and see the shop, rest, events and boss?
   - After onboarding, do players notice the enemy side and the guard?
   - Do they know what their relics do?
   - Do the Cultist's counter-attack and the High Priest's last stand read as the enemy fighting back?

## Build and distribution

In Unity, run **RhythmRogue > Build Playtest**. It makes a Windows development build (so testers get F8 bug reports from `PlaytestReporter`), names it after the version, date and commit, adds a `BUILD.txt` with tester instructions, zips it, and opens the folder. Send the zip with the solo form, and ask testers to copy the build name into the form's Build version field. It warns you first if there are uncommitted changes, since that build can't be traced to a commit.
## Architecture notes

- The death latch in `HealthComponent` is what caused the restart bug. Last Stand (Sept 22) now revives through a controlled path on top of it, so any change to the latch should be tested against both.
