# (C) Roadmap and Backlog

> What is on the horizon. Pulled from past sessions and open PROTO tickets. Not committed work, a menu of what is next. Updated November 23, 2026.

## Immediate

- See [[01 In Progress/(C) Update 1 - Variety and Encounters]] for the active work. (The player character system that sat here is done, July 2.)

## Open PROTO tickets

- **PROTO-025:** Code cleanup and architecture pass. Remove dead code (UsageExamples files, leftover test scripts like MapTest), audit singletons and event subscribe/unsubscribe pairs, finish the BattleConfig -> RunState migration, add `[DisallowMultipleComponent]` where appropriate.
- **PROTO-026:** In-editor chart authoring tool. A Unity charting editor to create JSON files and place notes and sliders against the map.
- **PROTO-027:** Rebindable keybinds via the Unity Input System rebinding workflow, stored in PlayerPrefs, for rhythm lanes and UI navigation. **Closed** (`KeybindManager`, confirmed September 22).
- **PROTO-028:** Full keyboard and gamepad UI navigation across all screens (W/A/S/D or arrow keys), so the mouse is never required. **Closed** (`UI/Navigation`, confirmed September 22).

## Systems and content

- Assembly Definition files (planned, roughly a 20-minute task, add when recompile times start to hurt).
- ~~`ReviveEnemy` on `IBattleContext` for the last-stand modifier.~~ Done with Last Stand, September 22.
- More enemy types with distinct modifiers (Speed, Trick, Chaos). Mostly content, not code, since EnemyData already supports it. This is the fastest way to make map path choices feel meaningful, since right now most fights are the same enemy. Counter-Attack and Last Stand exist now (September).
- ~~Event nodes: risk/reward choices to break up the battle-to-battle cadence.~~ Done, 8 events.
- ~~Shop node completion.~~ Done, working shop.
- Calibration screen (GDD prototype scope; JudgmentSystem already reads the offset from PlayerPrefs, so it mainly needs a tap-along UI).
- Save system, needed for Continue Run and meta-progression.
- Meta-progression system (Score currency, unlockable relics / songs / pattern packs / cosmetics).
- Boss battles with unique mechanics (tempo changes, lane swaps, multi-phase, counter-attack survival phases).
- Additional songs, pattern packs, music genres.

## Difficulty ladder (from the Ascension research doc)

Verdict on file: build a post-run difficulty ladder that is primarily a **strategy / economy** ladder, not a mechanical-rhythm one. A linear cumulative ladder of 8 to 12 tiers gated by beating the run, each adding one legible modifier, difficulty spread evenly (never two big knobs on one rung). Keep the mechanical floor (timing-window forgiveness, scroll speed) as a free toggle, not a locked rung. Optionally a Hades-style a-la-carte menu of rhythm mutators for self-expression. This is a later-stage feature; do not build it while content is still thin.
