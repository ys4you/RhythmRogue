# (C) Tools and Resources

> External tools, services, and their known quirks. Extracted from past sessions on July 1, 2026.

## How Claude reaches the project

This vault lives inside the repo (`obsidian-coworker/`), so any Claude that can open the repo can read and update it.

- **Claude Code** started in the repo or in the vault folder picks up the context automatically: the repo-root `CLAUDE.md` pulls in this vault's `CLAUDE.md`. It can run git and read the code directly.
- **The Claude app with your computer linked** reaches the repo through the linked folder and can run git there too.
- **The old setup (March to September)** was Claude Desktop with the Filesystem MCP tools only, no git. The tips in [[04 System/(C) Architecture and Conventions]] still apply when using those tools.
## Audio analysis (Claude's bash)

Claude's own bash environment has `ffprobe`, `ffmpeg`, and `librosa` available (`pip install librosa --break-system-packages`). Claude can analyze uploaded `.mp3` files from `/mnt/user-data/uploads/` directly. This is the preferred path for audio work such as tempo and onset analysis for beatmaps. Seven synthwave battle tracks were analyzed this way and assigned per enemy.

## Beatmap tooling (in the repo)

- `python/beatmap_generator.py` builds beat map JSON from a song, for example `python python/beatmap_generator.py song.wav --bpm 135`. The JSON for the four current songs sits next to it.
- `Tools/beat_analyzer.py` analyzes a song: `python Tools/beat_analyzer.py song.ogg`.
- `Editor/BeatMapImporter.cs` imports the JSON into a `SongBeatMap` asset in Unity.
- With Nix: `nix develop` gives a shell with Python, librosa and ffmpeg. `nix run .#beat-analyzer -- song.ogg` and `nix run .#beatmap-generator -- <args>` run the scripts directly.
## GitHub to Discord webhook

- Configured with a `/github` suffix on the Discord webhook URL (`https://discord.com/api/webhooks/<id>/<token>/github`).
- The Secret field is optional and irrelevant here, since Discord's `/github` endpoint does not verify HMAC signatures.
- Discord ignores ping events, so a ping test shows a payload but no message. Test with an empty commit instead.
- Currently subscribed to `["*"]` events, which is noisy. Candidate for trimming to pushes, pull requests, releases, and issues.
- The webhook URL is a credential. Never paste it into any chat.

## GitHub API

`api.github.com` is blocked from Claude's container, so Claude cannot call the GitHub API directly.

## GitHub repo to Claude Project sync

Obsolete: Claude now reads the repo directly. (In April, syncing the repo into a Claude Project hung for 30-plus minutes; pasting files into chat was the workaround.)
## Task tracking

Everything lives in this vault (decided November 23, 2026): the active update's checklist in `01 In Progress`, the backlog in `00 Ideas`, bugs and feedback in `02 Playtesting`. Notion is no longer used. The PROTO-xxx numbers in older notes came from the old Notion board.

The GDD (`gamedesigndocument.pdf`) is frozen as the v1 reference. Where it disagrees with this vault, the vault wins.

## Playtest builds

In Unity, **RhythmRogue > Build Playtest** (`Editor/PlaytestBuilder.cs`) makes a Windows 64-bit development build, so testers get `PlaytestReporter`'s F8 bug report. It names the build after version, date and commit (for example `RhythmRogue_v1.3_2026-12-10_77bd03f`), writes a `BUILD.txt` with tester instructions, zips it without the `DoNotShip` debug folders, and opens the folder. Output is in `Builds/Playtest/` (git-ignored). If there are uncommitted changes it asks first and adds `-dirty` to the name. Needs the Windows Build Support module in Unity Hub (relevant on the Linux laptop).

## Session start check (Claude Code)

`.claude/settings.json` runs `.claude/hooks/vault-check.sh` when a Claude Code session starts in the repo. If the vault's **Last documented commit** is behind `git log`, or there are uncommitted changes under `Assets`, it tells Claude to offer the session-wrap skill. It prints nothing when the vault is up to date. Runs under Git Bash on Windows.
## Discord playtest server

A community server for the game with playtesters: roles, a bug report forum, a feedback template, a session-notes template, and drafted server rules covering conduct, bug report standards, and playtest build redistribution. Copy bug reports and feedback from Discord into `02 Playtesting` so everything is in one place.
