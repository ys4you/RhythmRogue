# (C) Update 1 - Variety and Encounters

> The active update and the scope anchor. If work drifts, come back here. Spine chosen July 1, 2026, re-planned September 22, updated November 23.

## Goal (definition of done)

Two consecutive runs feel meaningfully different from each other: enemies that behave differently, real event nodes, and a boss that feels like a boss.

## Progress

- [x] **Event nodes.** 8 events authored, pooled, and routed end to end (confirmed September 22).
- [x] **Last Stand modifier** on the High Priest. Refuses its first death, comes back at 30% max HP, flashes "It refuses to die" with a sting, and raises chart difficulty for the second phase. Skips the revive if the player is below 15% HP. Built September 22.
- [x] **Counter-Attack modifier** on the Cultist. During chosen song sections (choruses by default) the enemy plays bursts of notes on the song's real onsets. They only hurt while your guard is down, so it raises the cost of a mistake rather than the speed. Built September 25. A `CounterAttack_HighPriest` asset also exists but is not attached.
- [ ] **Play it yourself.** First thing back: open Unity, confirm it compiles, play 3 full runs. See [[02 Playtesting/(C) Known Bugs and Playtest Plan]].
- [ ] **External playtest round 2.** Target: Sunday December 13 (proposed). 2 to 3 testers on a build from **RhythmRogue > Build Playtest**. Main question: is the early difficulty wall gone?
- [ ] **Lane mirror** (the "Trick" enemy) as a chart-generation transform: an `EnemyData` flag read at assembly time, no runtime context change.
- [ ] **Decide:** ship Update 1, or add a Speed enemy (pure config: a fast, dense song) first.

Neither modifier has been played by anyone but you yet.

## Why the playtest comes before lane mirror

Reordered November 23. The September plan was lane mirror, then playtest. But both June testers hit the same early difficulty wall, and the fixes for it (June 14 to 21) plus both new modifiers have never been tested by anyone. Lane mirror makes fights harder on top of an unverified curve. Test first so the next work is aimed at real feedback. Logged in [[04 System/(C) Decision Log]].

## Moved to Update 2

- Second area and area chaining (a boss win currently ends the run).
- Boss mechanics beyond Last Stand (tempo change, lane swap, multi-phase).
- Save system plus meta-progression, built together.

## Escape hatch

If it starts feeling heavy, ship what you have. Three enemies that play differently (Acolyte plain, Cultist counter-attacks, High Priest last stand) is already a valid update. Always keep a shippable state.

## Code notes

- Scroll speed is global (units per second, stored in PlayerPrefs), not per enemy.
- A new enemy must be added to `EnemyPool` to appear. `minDepthT` gates it by normalized map depth.
- `IBattleContext` grew in September to support both modifiers (section and onset queries, the revive, chart escalation). Lane mirror still needs either a chart-time transform (preferred) or a new context method.
- Modifiers are cloned per fight, so shared assets are never mutated.
- Modifier assets live in `Assets/_Project/SCO/Modifiers`.

Full September audit: [[07 Iteration Logs/(C) Reboot Audit - September 2026]].
