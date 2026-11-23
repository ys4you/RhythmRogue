# RhythmRogue Dev Log — 2026-06-03

A handoff log for the next chat. Covers everything done this session. Read this first.

---

## Quick status

- Full 8-scene loop works: MainMenu, Map, Battle, Rest, Reward, Summary, Shop, Event.
- User did a clean full playthrough this stretch. Only outstanding gap is **real relic art** (placeholder system is in place).
- This session was mostly: shared SettingsPanel refactor, relic-view HUD (bar + detail card), relic icon field, battle music sourcing + analysis + assignment, and fixing the beatmap generator. Plus several real bug fixes the user found by playing.
- **Highest-value next step remains an external playtest** (3 testers — GDD success criterion). User has been building/polishing instead; that's a legitimate choice but the recommendation stands.

### Machine paths (call `Filesystem:list_allowed_directories` first each session)
- Laptop (active this session, mostly): `C:\PersonalProjects\RhythmRogue`
- Desktop: `C:\Users\Computerpilot\Documents\Github\RhythmRogue`
- Claude CANNOT run git or terminal on the user's machine. User does all commit/push, and runs the Python generator themselves (or Claude runs it on Claude's own environment against uploaded files).
- Two-machine sync gotcha: sprite/.meta refs and file changes must be committed+pushed or they appear missing on the other machine. Happened twice this session (icon refs "not being pushed"; icon changes briefly thought to be desktop-only but were actually on laptop).

---

## Bug fixes this session (all found by the user playing)

1. **Boss win didn't grant victory.** `BattleResultHandler` decided boss-ness via `EnemyData.IsBoss` (which is `maxHP >= 250`). If the boss asset had <250 HP or null EnemyData, the boss win routed to the reward screen as a normal win and victory was never granted — and since boss is the last node, the run just ended. FIX: decide boss-ness from `NodeType.Boss` (set authoritatively by the map generator), with `EnemyData.IsBoss` kept only as a fallback. NOTE: boss asset HP may still be low (own balance issue — GDD wants 250-400).

2. **Hit notes scrolled past the receptor.** Player highway only despawned notes that scrolled off the bottom, so a hit tap note kept moving down. FIX in `NoteHighway`: `DespawnPassedNotes` despawns `IsHit` notes immediately; `ScrollActiveNotes` skips repositioning hit notes (`if (note.IsHit && !note.IsBeingHeld) continue;`). Hold notes stay alive while `IsBeingHeld`; missed notes still scroll past (correct — a miss should fly by, a hit should vanish).

3. **Pause menu opened then instantly closed (panel left inactive).** Both `GlobalPauseManager` and the PauseMenu's own `UICancelHandler` read `Keyboard.current.escapeKey.wasPressedThisFrame` in the same frame, so the Escape that opened the menu was consumed again and fired resume. FIX: `UICancelHandler.SuppressForOneFrame()` (`_suppressUntilFrame = Time.frameCount + 1`, Update skips while suppressed); `PauseMenu.Show()` calls it. Also kept the earlier fix of building PauseCanvas as a ROOT DontDestroyOnLoad object (a nested overlay canvas could render at zero size). Diagnostics removed. CONFIRMED WORKING by user.

---

## Shared SettingsPanel refactor (DONE, committed)

[DECISION] Pause-menu Settings needed full parity with main-menu Settings (Audio + Controls + Display), not just sliders. Chose **shared component, no duplication** (SOLID).

- NEW `Scripts/UI/SettingsPanel.cs` — full tabbed settings (Audio: scroll speed / audio offset / master / music / sfx; Controls: 4-lane rebind via KeybindManager, reset defaults, conflict warning; Display: resolution dropdown / window mode / vsync / CRT toggle). API: `Build(parent, focusSetter, cancelHandler)`, `Open()`, `Close()`, `OnCloseRequested` event, `IsOpen`. Manages its own cancel-stack entry (`_cancelPushed`): Escape pops via the stack (`RequestClose`), the Back button calls `CloseFromButton()` which pops then closes.
- `MainMenuScreen.cs` — slimmed; removed ~400 lines of inline settings, delegates to SettingsPanel.
- `PauseMenu.cs` — slider-only group replaced by the shared SettingsPanel.
- Both build the panel AFTER focus + cancel handlers exist.

### Compile gotcha fixed
`_root` in SettingsPanel is typed `RectTransform`; `SetActive`/`activeSelf` are `GameObject` members. CS1061 fixed by using `_root.gameObject.SetActive(...)` / `.gameObject.activeSelf` (3 spots in Open/Close/IsOpen). (Same RectTransform-has-no-SetActive lesson as before.)
Also CS0104 risk: `RhythmRogue.Core.Audio.AudioSettings` vs `UnityEngine.AudioSettings` — fully-qualified + `using AudioSettings = RhythmRogue.Core.Audio.AudioSettings;`.

---

## Relic-view HUD: bar + detail card (DONE, committed)

[DECISION] Slay-the-Spire-style: always-visible icon strip on the HUD; hover shows the relic name; click opens a full detail card (rarity, name, effect, description, flavor).

- NEW `Scripts/UI/RelicBar.cs` — self-contained own canvas (sortingOrder 120), top-center icon row, reads `RunState.ActiveRelics`. `RelicBar.Create(runState)`. Hover tooltip via nested `RelicIconHover` (IPointerEnter/Exit). Click opens detail. `Refresh()` rebuilds. Each icon = rarity ring + accent swatch + (now) the resolved icon sprite.
- NEW `Scripts/UI/RelicDetailCard.cs` — modal canvas (sortingOrder 800), rarity border/icon/name/effect/desc/flavor, dismiss via backdrop / Close / Escape (uses UICancelHandler push/pop).
- `MapScreen` creates the bar after building the map (stored as `_relicBar`).
- Battle HUD intentionally omitted (clutter during rhythm gameplay). Adding later is one `RelicBar.Create` call.
- Canvas sorting reference: map=50, relicbar=120, pause=500, relic detail=800, CRT=10000, fade=9999.

---

## Relic icon field + placeholder fallback (DONE, committed)

[DECISION] Add an `icon` Sprite field to RelicData; when empty, fall back to a shared placeholder = the map's event "?" sprite (`Resources/MapIcons/node_event`), mirroring how MapUI auto-loads node icons.

- `RelicData.cs` gained: `flavorText` (italic cosmetic), `icon` Sprite field, `ResolvedIcon` property (`icon ?? PlaceholderIcon`), static cached `PlaceholderIcon` loading `Resources.Load<Sprite>("MapIcons/node_event")`.
- ALL FOUR relic display surfaces now layer `ResolvedIcon` over the colored swatch: `RelicBar.BuildIcon`, `RelicDetailCard` (`_iconSprite`), `RewardPickScreen.CreateRelicCard` (over IconBG), `ShopScreen.CreateShopCard` (over IconBG). All `preserveAspect`, `raycastTarget=false`.
- IMPORTANT lesson: the reward + shop screens build their OWN cards, so the icon change had to be applied to them separately (they were initially missed — user reported "images not shown in shop and reward screen"). Any future relic-display change must touch all four surfaces.
- Real icon assignment is Inspector-only later (drop a sprite into each relic's `icon` field). Until then every relic shows the "?" placeholder.

---

## Battle music — sourcing, analysis, assignment (this was the big focus)

### The vibe decision
- [DECISION] Dark "ritual ambient" is beatless background — UNCHARTABLE. A rhythm game needs music with a clear, steady, detectable beat. Right genres: **darksynth / horror-synth** (best fit), industrial/EBM, gothic instrumental metal, witch house. Search with beat-implying terms, not "ambient".
- Aesthetic caveat: the tracks the user found are **synthwave/cyberpunk**, not strictly gothic/occult. Acceptable placeholder; the minor-key ones read as "dark and driving". Whether synthwave fits is a **playtest question**, not a now-decision.
- Sourcing for prototype: Pixabay (free commercial), Incompetech (CC-BY), OpenGameArt, FMA, itch.io packs. Verify license + record source before shipping.

### Tracks the user has (7 uploaded, analyzed via librosa on Claude's env)
Measured BPM / character (darkness from spectral centroid + major/minor):
- **Stellar Saga** ~99 — DARKEST, minor (name misleads; it's the best dark fit)
- **Fight For The Future** ~112 — mid, minor
- **Digital Driving Force** ~112 — dark, MAJOR
- **Neuro-Synth** ~117 — punchiest/loudest pulse, minor (most tappable)
- **Crash Runner** ~144 — dark, minor
- **Bionic Beatdown** ~144 — BRIGHTEST, MAJOR (least on-vibe)
- **Obsidian Overdrive** ~161 — fastest, minor, shortest (~118s)
All have tight, steady beats — all chartable. No technical duds.

### Enemy data (read from `Assets/_Project/SCO/Enemy/Enemies_data/`)
The three non-boss enemies are a graded cult trio (same theme, difficulty via bpmModifier):
- **acolyte** — markerDifficulty 0.3, HP 80, bpmModifier **0.85** (slows song)
- **Cultist** — 0.4, HP 100, bpmModifier **1.0** (native)
- **zealot** — 0.6, HP 90, bpmModifier **1.25** (speeds song up)
- **High Priest** — the boss.
CURRENTLY all three share one song + songBeatMap (need per-enemy assignment).

### FINAL music → enemy mapping (accounts for bpmModifier so post-modifier BPM lands in the right band)
- **Acolyte → Stellar Saga** (~99, ×0.85 ≈ 84, sparse ritual; sensitivity 0.4)
- **Cultist → Neuro-Synth** (~117 native, punchy baseline / default battle track; sensitivity 0.5)
- **Zealot → Crash Runner** (~144, ×1.25 ≈ 180 = top of Speed band; sensitivity 0.6) — NOT Obsidian Overdrive (×1.25 would hit ~200, too brutal)
- **High Priest (boss) → Obsidian Overdrive** (~161 full intensity, no modifier; sensitivity 0.6)
- Spare for variety later: Fight For The Future, Digital Driving Force, Bionic Beatdown (prefer minor-key ones; major-key clash most).
- Boss caveat: a single steady-BPM loop can't do GDD's tempo-change/multi-phase boss mechanics. Obsidian Overdrive is a placeholder; real boss music is post-playtest.
- [DECISION] 7 tracks is MORE than enough for the prototype. Do NOT hunt for more music.

### Files
- Audio lives at `Resources/Audio/Songs/` (NOT `Music` — that's menu/map ambient: MenuDrone, MapShamanic). User renamed tracks in that folder to: `Stellar Saga.mp3`, `Neuro-Synth.mp3`, `Crash-Runner.mp3`, `Obsidian Overdrive.mp3` (plus the unused 3, plus test files). WATCH: renaming in Explorer (not Unity) can orphan .meta and change GUIDs → enemy `song` refs may go blank; reassign in Inspector if so.
- Assignment notes doc written to `Resources/Audio/Songs/BATTLE_MUSIC_NOTES.md`.

---

## Battle-start dead-air fix (DONE)

User: with longer tracks, "takes too long until I can play." Two stacked causes:
1. `BattleManager._introDelay = 2f` — flat 2s before the song starts every fight.
2. Beatmap's first section was `Intro / EnemyOnly` spanning the entire first 16-beat block — long on longer songs, so player notes start very late.

[DECISION from user] KEEP a short intro (they like that notes don't slam onto a lane the instant the song starts), but make it short. Apply BOTH levers.

- `BattleManager._introDelay` 2f → **1f** (now has a tooltip; serialized — scene-saved value may override, set on component if still long).
- `python/beatmap_generator.py`: added `--intro-beats` arg (default 4 = one bar). Intro is now a short fixed enemy-only block at the very front, THEN the rest of the song sections normally. First body section comes in `PlayerOnly` (ease-in). `leadInBeats` now tracks intro length. `--intro-beats 0` starts immediately; `--intro-beats 2` for an even shorter intro.

---

## Beatmap generator BUG (the big one — FIXED in generator, beatmaps must be regenerated)

**Symptom:** user's generated beatmaps had EVERY section `EnemyOnly` — Stellar Saga was 18/18 enemy-only, player never got notes. All four tracks affected.

**Root cause:** section energy was computed as `section_mean / song_peak`. A mean is always far below a single peak, so every section scored ~0.1, below the 0.3 "Break" threshold → every section classified Break → EnemyOnly. Latent in the original generator; longer tracks made it obvious. (This likely means the generator NEVER produced good player sections.)

**Fix (in `python/beatmap_generator.py`):** compute all body sections' raw mean energies, then normalize each against the MIN-MAX range across sections, so quiet vs loud actually separate across 0-1 and the thresholds work. Thresholds tuned to >0.66 Chorus/Both, <0.2 Break/EnemyOnly, else Verse/PlayerOnly; last section Outro/EnemyOnly.

**Verified** (Claude ran the fixed generator on Claude's env): Stellar 3 EnemyOnly / 9 PlayerOnly / 6 Both; Neuro 5/9/13; Crash 8/19/8; Obsidian 8/9/3. Healthy mixes.

**ACTION FOR USER (pending):** re-run the four generator commands on the laptop (needs `pip install librosa soundfile numpy`). The 4 JSONs the user uploaded earlier were from the BROKEN version — replace them. Fixed generator is already saved in the project.

### Correct generator commands (post-rename, post-fix)
```
cd C:\PersonalProjects\RhythmRogue\python
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Stellar Saga.mp3" --bpm 99 --sensitivity 0.4 --output stellar_saga_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Neuro-Synth.mp3" --bpm 117 --sensitivity 0.5 --output neuro_synth_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Crash-Runner.mp3" --bpm 144 --sensitivity 0.6 --output crash_runner_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Obsidian Overdrive.mp3" --bpm 161 --sensitivity 0.6 --output obsidian_overdrive_beatmap.json
```
Output JSON lands in the `python/` folder. Always pass `--bpm`. NOTE: librosa BPM can be half/double — if a chart scrolls at half/double the music's pace in-engine, halve/double that track's BPM and regenerate.

---

## How the generator + chart system relate (important context)

- The generator places markers straight from audio onsets = the "algorithm decides where every note goes" approach the user deliberately MOVED AWAY FROM (the HybridAssembler principle: human-authored patterns placed algorithmically; algorithm decides when/which pattern, never where individual notes go).
- So treat generator JSON as a **scaffold** (BPM, section player/enemy/both breakdown, rough density, downbeats), then refine feel in-engine with hand-authored patterns. The section-mix fix is the part that was genuinely broken.
- `--sensitivity` (0-1) = density dial (threshold = 1 - sensitivity; only affects extra onsets beyond the main beat). Maps to enemy difficulty/density: lower for slow/ritual enemies, higher for fast/aggressive. Default 0.5.
- Name JSON by TRACK, not enemy (beatmap belongs to the song; the enemy applies its own bpmModifier + markerDifficulty). Generate at NATIVE track BPM — the Conductor rescales by bpmModifier at runtime.

### Battle-start flow (for reference)
`BattleManager.Awake` stops MapShamanic, resolves enemy + chart via `ChartProvider.Resolve` (uses `songBeatMap` if present, else `RuntimeBeatAnalyzer` audio analysis). FSM: Intro (waits `_introDelay`) → Playing (`conductor.Play(EffectiveBPM, AudioOffset)`) → Won/Lost (`_endDelay`=2f). `ChartProvider.ResolveFromBeatMap`: effectiveBPM = `beatMap.bpm × bpmModifier`.

### SongBeatMap shape
Fields: songName, bpm, clip, audioOffsetSeconds, sections[], markers[], leadInBeats (4), tailBeats (2). SongSection: label, startBeat, endBeat, type (SongSectionType), highway (SectionType = EnemyOnly/PlayerOnly/Both), intensityScale. BeatMarker: beat, type (Kick/Snare/HiHat/Accent/Melodic/Break), intensity, direction, holdBeats.

---

## Playtest infrastructure (set up this session, in Discord)

[DECISION] Roles (palette hex; avoid dark tones invisible on dark theme): Developer = Administrator, WarmGold #F0CE6E; Playtester = AmberOrange #EAA44A (View/Send/Embed/Attach-files/React/History/Connect/Speak); Bug Hunter (optional reward) = RustOrange #B15B35; Bot = BgLight #703C67; @everyone locked down. Role order Developer→Bot→BugHunter→Playtester→@everyone.

[DECISION] Bug reporting = **open Forum channel** (not a ticket system) at 3-tester scale, so testers see frequency and pile onto the same bug. Tags: Battle/Map/Shop/Event/Rest/Reward/Pause-Settings/Audio/Other. Separate #feedback channel for non-bug impressions.

[DECISION] No save-data in bug reports — there's no save system. Repro method is the **RUN SEED** (shown on map + summary, format XXXX-XXXX). Claude wrote a plain-spoken pinned bug-report post + an EXAMPLE report (marked do-not-reply, used the real fixed "Boss no victory" bug, seed WZEX-MTJN).

[DECISION] Feedback report template: `{date} {name}` + `Type: [Bug/Balance/Confusing/General]` + `Got to:` + `Play background:` + freeform. The Type tag is the key structuring addition.

[DOCUMENT] Playtest session-notes template at `Playtesting/_TEMPLATE.md` — Session info / 1.OBSERVE (silent watch) / 2.DURING (sparse neutral prompts) / 3.AFTER (open-first then targeted; includes the neutral enemy-highway question) / 4.takeaways. Copy per session as `YYYY-MM-DD_name.md`.

---

## Recurring guidance Claude gave (so next chat is consistent)

- **The next step is a playtest, not more features.** GDD success = 3 external testers complete a run without blocking bugs; every precondition is met. User keeps choosing to build/polish. Reasonable to scale DOWN (watch 1 player, or a structured solo run) rather than skip. The user's own solo playthroughs found 4 real bugs — proof the loop is worth testing.
- **Save/resume mid-run: DEFERRED.** It's a published-game feature (single-slot suspend-save, "Continue Run" per GDD), benefits from knowing run length first, and RunState already centralizes the serialization target so deferral costs nothing. Don't build before playtest. Worth it for shipping, not now.
- Missing relic art does NOT block a playtest — placeholder swatches + "?" icon are fully informative.
- Highest-impact polish for elevating the game = battle GAME FEEL / juice (screen shake scaled to judgment, particles on Perfect, receptor punch, hitstop) — but it needs in-engine by-feel tuning, so it's not ideal "train/away-from-machine" work. User has existing `HitFeedback.cs` + `HitParticles.cs` (not yet assessed).

---

## Pending / TODO (carry into next chat)

1. **Re-run the 4 beatmap generator commands** (above) on the laptop — the uploaded JSONs were from the broken generator. Needs librosa installed. Alternatively Claude can run the fixed generator on Claude's env against the uploaded mp3s and hand over JSON.
2. **Import the 4 beatmaps + wire per-enemy songs.** Currently all 3 enemies share one song/beatmap. Assign each enemy's `song` + `songBeatMap` to its own track per the FINAL mapping. bpmModifiers already set per enemy. Set each track's constant BPM. Verify in-engine the BPM isn't half/double.
3. **Test battle-start timing** — confirm the 1s `_introDelay` + one-bar intro feels right; tune `--intro-beats` (try 2) and `_introDelay` by feel.
4. **Assign real relic icon sprites** to each relic's `icon` Inspector field (user has art on laptop) — currently shows the "?" placeholder. Inspector-only, no code.
5. **Raise boss HP** if still low (GDD wants 250-400) — separate from the victory fix.
6. **Commit + push from laptop** — pause/settings refactor, relic bar/detail card, relic icon field (all 4 surfaces), battle-start fix, generator fix, BATTLE_MUSIC_NOTES.md, this dev log. Use the per-feature commit messages already given, or one combined.
7. Optional housekeeping: delete deprecated `MapPauseController.cs` stub in Unity; ensure only ONE `GlobalPauseManager` in the startup scene. (From earlier sessions — may already be done.)
8. **Recurring:** cut a Windows build, get to 3 testers.

---

## Architecture reminders (unchanged, for safety)

- SOLID, ScriptableObject configs, seeded determinism (SeededRandom / RunSeed.GetRandom(RandomDomain, int)), code-generated UI (no prefabs), two-layer events (C# + EventBus), `GameLog` (Info/Warn/Error — NOT Warning), `[Conditional]` so zero release overhead.
- RunState (SO) = canonical cross-scene store at `Assets/_Project/SCO/RunState.asset`.
- **Active Input Handling = "Input System Only"** — legacy `UnityEngine.Input` is DEAD; always read `Keyboard.current` / `Mouse.current` / `Gamepad.current` under `#if ENABLE_INPUT_SYSTEM`. (Bit the pause system AND UICancelHandler this stretch.)
- RectTransform has no `SetActive`/`activeSelf` → use `.gameObject` (CS1061).
- `RhythmRogue.Core.Audio.AudioSettings` vs `UnityEngine.AudioSettings` → fully-qualify (CS0104).
- Two readers consuming the same `wasPressedThisFrame` in one frame → open-then-close race → one-frame suppression guard.
- Palette: BgDeep #150C21, BgSurface #332232, BgLight #703C67, Shadow #5A2D2A, RustOrange #B15B35, AmberOrange #EAA44A, WarmGold #F0CE6E, OffWhite #FCF8F4. NO red.
- Dark/occult/gothic aesthetic (Castlevania-to-Inscryption). NOT FNF-styled.
- Working style: terse, iterative; tests in-engine and reports back; prefers full file rewrites but minimal output when simple; decision fatigue → Claude should pick a direction and give commit messages.
