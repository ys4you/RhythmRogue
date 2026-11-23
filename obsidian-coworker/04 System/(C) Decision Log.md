# (C) Decision Log

> One line per decision: what was picked and why, so settled questions stay settled. Newest last. Add a row whenever you pick an approach, cut scope, or reorder work (the session-wrap skill does this). Created November 23, 2026 from the existing notes.

| Date | Decision | Why | Source |
|---|---|---|---|
| Standing | Code-generated UI and visuals over prefabs, `SeededRandom` only, `GameLog` over `Debug.Log`, dependencies point Data <- Battle <- UI, ScriptableObjects for config | One swap point per element, deterministic runs, zero-cost logging in release | [[04 System/(C) Project Overview]] |
| Standing | The prototype is validated when 3 external testers finish a full run with no blocking bugs. Don't move the goalpost mid-test | GDD success criterion | [[02 Playtesting/(C) Known Bugs and Playtest Plan]] |
| Mar 13 | The utility layer is generic and ships zero game-specific enums | Keeps `Util` reusable, free of game knowledge | [[07 Iteration Logs/(C) Session History]] |
| Mar 21 | Removed the static `BattleConfig`; cross-scene data lives in `RunState` | Mutable static state was a prototype shortcut | [[04 System/(C) Architecture and Conventions]] |
| Apr 3 | Relics are pure-data ScriptableObjects, `RelicApplier` is stateless, boss wins skip the reward screen | Not recorded | [[07 Iteration Logs/(C) Session History]] |
| Apr 4 | Charts are human-authored fragments placed by the algorithm. It decides when and which fragment, never where individual notes go | Fully generated charts (PhrasePlanner, VoiceMap) felt robotic | [[04 System/Codebase/(C) Chart Generation]] |
| Jun 8 | Guard mechanic: enemy notes only hurt while your guard is down | Playtest 1: "the enemy just exists" | [[07 Iteration Logs/Dev Logs/2026-06-08_session-log]] |
| Jun 14 | Difficulty comes from the area's band, node depth and run tier (`DifficultyCurve`) | Not recorded. Came the day after Playtest 2 hit the difficulty wall | git `ddb9e12` |
| Jun 16 | Chart density targets notes per second, not BPM | Charts stay consistent across tempos | git `255715f` |
| Jul 1 | Scroll speed is global and constant (units per second), not per enemy | Not recorded. Consequence: a "Speed" enemy is a fast, dense song, pure config | [[01 In Progress/(C) Update 1 - Variety and Encounters]] |
| Jul 1 | Update 1 spine: variety and encounters. Escape hatch: ship once three enemies play differently | Always keep a shippable state | [[01 In Progress/(C) Update 1 - Variety and Encounters]] |
| By Jul 1 | The difficulty ladder will be a strategy/economy ladder, not a mechanical one, and waits until content is less thin | Ascension research doc | [[00 Ideas/(C) Roadmap and Backlog]] |
| Jul 2 | One shared animator behind `ICharacterVisual` for player and enemies | One system to maintain; enemies animate without extra art | [[03 Done/(C) Player Character System]] |
| Jul 2 | Character art in tiers. Ship on single-sprite posing first, invest in art by screen-time | Sing poses are charm; the arrows carry the timing | [[04 System/(C) Character Art Scope]] |
| Sep 22 | Counter-Attack and Last Stand before Trick and Chaos | They need little or no new `IBattleContext` surface, so they're the smallest real wins | [[07 Iteration Logs/(C) Reboot Audit - September 2026]] |
| Sep 22 | Second area, meta-progression and save system move to Update 2. Save is built together with meta-progression | Keeps Update 1 finishable | [[07 Iteration Logs/(C) Reboot Audit - September 2026]] |
| Sep 25 | Counter-Attack went on the Cultist, Last Stand on the High Priest | Not recorded. Fits Playtest 1, where the Cultist felt the same as the Acolyte | git `f7dae46` |
| Nov 23 | The vault covers RhythmRogue only and lives in the repo as `obsidian-coworker/` | Everything about the game in one place Claude can read and update | [[07 Iteration Logs/(C) Session History]] |
| Nov 23 | Playtest round 2 comes before lane mirror (Claude's call, change it if you disagree) | The June fixes and both modifiers are untested, and lane mirror adds difficulty on top of an unverified curve | [[01 In Progress/(C) Update 1 - Variety and Encounters]] |
| Nov 23 | Notes and tasks live only in this vault. Notion is retired, and the GDD is frozen as the v1 reference | Fewer places to keep updated; the vault wins where they disagree | Yesse, this session |
| Nov 23 | Next playtest targeted for Sunday December 13 (proposed by Claude, change it if it doesn't fit) | A fixed date to work toward, before the holidays | [[CLAUDE]] |
