# (C) Project Overview

> The master reference for RhythmRogue. Extracted from past sessions on July 1, 2026.
> Note: the game code is the Unity project this vault sits in. This vault is for design, thinking, and tracking.

## What the game is

A rhythm roguelike on the premise "what if Friday Night Funkin' had roguelike elements." Four-lane note-highway rhythm gameplay (FNF / Fortnite Festival style) fused with Slay the Spire / Inscryption run structure. Charts are assembled dynamically from hand-authored pattern fragments, so every run plays differently. Solo indie project, targeting a content-complete v1.0 on Steam, with a possible demo as a milestone.

## Tech stack

- Unity 6, URP, 2D
- Target resolution 384 x 216, 32 pixels per unit, pixel art
- Input System package only (legacy `UnityEngine.Input` is not used)

## Aesthetic

Dark, occult, gothic. Warm 8-color palette from BgDeep `#150C21` to OffWhite `#FCF8F4`. No red in the palette.

## Architecture principles (non-negotiable)

- Code generation over prefabs for all UI and visuals. The project does not hand-author `.prefab` assets; visuals are built in code so there is a single swap point per element.
- SOLID throughout.
- Dependency direction points one way: Data <- Battle <- UI. Lower layers never reference higher ones.
- ScriptableObjects for all config data.
- `SeededRandom` / `ISeededRandom` for every procedural system. Never `UnityEngine.Random`.
- `GameLog` instead of `Debug.Log`, so logging strips to zero overhead in release builds.
- Two-layer event system: plain C# events for high-frequency signals (Conductor beats), and an `EventBus` for game-wide events.
- Assembly Definition files are planned but not yet added.

## Scene inventory

Nine scenes, full run loop wired end to end:

1. MainMenuScene
2. MapScene
3. BattleScene
4. RestScene
5. ShopScene
6. EventScene
7. RewardScene
8. SummaryScene
9. ChartGymScene (test harness)

## Two machines, one project

The game repo is synced across two machines via git push and pull.

- Laptop: `C:\PersonalProjects\RhythmRogue`
- Desktop: `C:\Users\Computerpilot\Documents\Github\RhythmRogue`

Check `git status` and `git log` before editing, so you know which machine was last active and what is uncommitted. A Nix flake (`flake.nix`, November 20) provides a dev shell for working on Linux or NixOS.

## Reference documents on file

None of these are in the repo or vault yet, so Claude can't check them. <!-- TODO: drop the GDD and the Ascension research doc into 06 Attachments so Claude can convert them to markdown -->

- `gamedesigndocument.pdf` (the GDD, frozen as the v1 reference. Where it disagrees with this vault, the vault wins)
- `Ascension Ladders for Rhythm-Roguelikes` research doc (verdict: build a strategy/economy difficulty ladder, not a mechanical-rhythm one)
