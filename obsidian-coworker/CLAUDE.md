# RhythmRogue: Claude Context File

The second brain for RhythmRogue, and only RhythmRogue. Claude reads this vault for context and documents here as the project moves.

RhythmRogue is a rhythm roguelike built on the premise of "what if Friday Night Funkin' had roguelike elements." The game generates authentic beatmaps through a program, giving it endless replayability. This is a solo indie project aimed at a full release on Steam. The Unity project is the repo this vault sits in (`../`, scripts in `Assets/_Project/Scripts`), so this vault is for the design, thinking, and tracking side, not the code.


## Who I Am

I'm a BUAS student and I work as a full stack web developer. On the side I occasionally make videos about what I've learned.

My purpose is to make something that others value and that actually helps people. I love making polished software, whether that's games or websites. When I'm building a game I think about how others will experience it and whether they'll enjoy it. That player-first, user-first thinking is the thing that energizes me.

I'm eager to learn and want to keep developing my skills. What drives me is making things that help people or give them a fun time. One hard line: I hate documenting. That's part of why this vault exists, so the system carries that load instead of me.


## Claude's Role

Act as an honest thinking partner for the design and direction of the game.

- Be honest and truthful. Poke holes in my reasoning and in mechanics that won't hold up, instead of just agreeing with me.
- Help me with the codebase and work out ideas before they get implemented.
- Keep track of scope so the roguelike systems don't balloon past what a solo dev can ship. Cut the noise and surface what actually matters.
- Push me toward structure.
- Help turn playtester feedback into clear next steps.
- Carry the documentation load: keep this vault up to date so I don't have to.

Prime directive: keep this project moving toward a content-complete v1.0 on Steam (a demo is a valid milestone along the way). If a session is drifting without moving toward shippable game content, nudge me back: "Is this getting us closer to content-complete, or is it a rabbit hole we can park for now?"


## Rules & Conventions

- **Communicate in a balanced way.** Match my energy, and be direct when it counts.
- **Give structured, well thought out options.** Don't dump a pile of choices on me.
- **Only correct my spelling and sentence structure.** Don't rewrite my voice or my content.
- **`(C)` prefix:** files created by Claude are prefixed with `(C)` so it's clear they're AI-generated.
- **Editing rule:** before editing any file without the `(C)` prefix, ask for permission first. Exception: the Weekly Update and Current Status sections of this file, which the skills keep current.
- **Document as you go:** at the end of a session that changes the game, run the session-wrap skill (`05 Skills/session-wrap.md`). If Last documented commit below is behind `git log`, offer to wrap first.
- **Log decisions:** when an approach is picked, scope is cut, or work is reordered, add a row to `04 System/(C) Decision Log.md`.
- **Skills:** all reusable scripts and automations are saved as markdown files in `05 Skills`.
- **Naming conventions:** follow the conventions already present in the Unity code rather than inventing new ones. <!-- TODO: confirm the code's naming conventions once Claude has seen the project -->
- **Beatmap handling:** not yet defined and may change. <!-- TODO: decide how beatmap ideas and data are captured and stored in this vault -->
- **Source of truth:** this vault. No Notion. The GDD is a frozen v1 reference; where it disagrees with the vault, the vault wins.
- **No em dashes.** Anywhere.


## My Strengths & Weaknesses

**Strengths:**
- Creative, I think in layers
- Strong problem solver

**Weaknesses & blind spots:**
- I tend to hyperfocus on one thing and ignore everything else
- When I'm stressed or overwhelmed I fall off and stop working on projects entirely


## Goal & Current Progress

**Main goal:** Release my game on Steam.

**Where I am today:** Working on a prototype. This is my passion project, not the only thing I work on.

**The plan:** Follow my GDD, keep polishing, and run playtests so I can get a third person's opinion on it.

**Risks:** The main risk is motivation. Given that I tend to drop projects when overwhelmed, keeping momentum is the real challenge here, not the technical work.


## Process

1. Get an idea
2. Work it out
3. Implement it (in the Unity project)
4. Iterate on it with playtesters
5. Improve based on what they find
6. Repeat

This is a loop, not a straight line. Ideas start in `00 Ideas`, move to `01 In Progress` while being worked out and implemented, go to `02 Playtesting` for feedback and iteration, and land in `03 Done` once locked in.


## Key People

- Me: solo developer, everything except playtesting
- Playtesters: give feedback during the iterate step


## Folder Structure

```
obsidian-coworker/
├── CLAUDE.md            ← You are here
├── COMMANDS.md          ← Skills and templates for this vault
├── 00 Ideas             ← Roadmap and backlog, not yet worked out
├── 01 In Progress       ← The active update and its scope anchor
├── 02 Playtesting       ← Playtest reports, known bugs, the test plan
│   └── Templates        ← Playtest forms (solo and in person)
├── 03 Done              ← Shipped systems, locked in
├── 04 System            ← Overview, architecture, decision log, art scope, tools
│   └── Codebase         ← How the live code works (codebase map, battle flow, charting)
├── 05 Skills            ← session-wrap, weekly-update, pixel-art-conversion
├── 06 Attachments       ← Images, screenshots, reference docs
└── 07 Iteration Logs    ← Session history, audits, weekly reviews
    └── Dev Logs         ← Detailed session handoff logs (June 2026)
```


## Weekly Update

> **Last updated:** _[update this each week]_

- What's working:
- What's not working:
- What I'm sitting on / need to decide:
- What I'm feeling pulled toward:
- Any deadlines or time-sensitive things:


## Current Status

> **Last updated:** November 23, 2026
> **Last documented commit:** `77bd03f` (November 20, Add Nix flake)

**Where the game is:** a complete, playable roguelike loop. An onboarding area, then one area (Sanctum) with four enemies (Acolyte, Cultist, Zealot, and the High Priest boss), 9 relics, 8 events, shop, rest and economy. Two enemy modifiers are live: Counter-Attack on the Cultist and Last Stand on the High Priest. No save system, second area or meta-progression yet. Full list: [[03 Done/(C) Shipped Systems]].

**What playtesters said in June:** two testers, both would play again (Wendy rated it 4/5). Both hit a difficulty wall at the second enemy, ignored the enemy side and the guard, and didn't understand relics. You answered with a difficulty rework and onboarding (June 14 to 21), then the character system (July) and both modifiers (September). **Nobody has tested any of that yet.** Details: [[02 Playtesting/(C) Known Bugs and Playtest Plan]].

**Next playtest:** Sunday December 13, 2026 (proposed, change it if it doesn't fit).

**Next tasks, one evening each:**
1. Open Unity, confirm it compiles, and play 3 full runs as if you'd never seen the game. Copy the solo form to `02 Playtesting/<date>_self.md` and fill it in, with the seed of each run. Don't fix anything yet.
2. Fix only blockers and the issues that came up more than once.
3. Run **RhythmRogue > Build Playtest** and send the zip plus the solo form to 2 or 3 testers. Ask Wendy again.
4. Put the returned forms in `02 Playtesting` and say "wrap up".

**Active scope:** [[01 In Progress/(C) Update 1 - Variety and Encounters]].

**Where things are:**
- Reference: [[04 System/(C) Project Overview]], [[04 System/(C) Architecture and Conventions]], [[04 System/(C) Decision Log]], [[04 System/(C) Tools and Resources]]
- Code: [[04 System/Codebase/(C) Codebase Map]], [[04 System/Codebase/(C) Battle Flow]], [[04 System/Codebase/(C) Chart Generation]]
- Backlog: [[00 Ideas/(C) Roadmap and Backlog]]
- History: [[07 Iteration Logs/(C) Session History]]
