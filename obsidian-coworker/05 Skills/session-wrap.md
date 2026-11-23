# Skill: Session Wrap

Run at the end of a working session. Brings the vault in line with what actually changed in the code, so I never have to document by hand.

## When to Use

- I say "wrap up", "wrap the session", "document this", "done for today", or similar
- At the start of a session, if the **Last documented commit** in `CLAUDE.md` is behind `git log` (a session was never wrapped). Offer to wrap it first.

## Steps

### 1. Find what changed

From the repo root (the folder above `obsidian-coworker/`):

```bash
git log --oneline <last documented commit>..HEAD
git status --short
git diff --stat <last documented commit>..HEAD -- Assets/_Project
```

The last documented commit is in `CLAUDE.md` under Current Status. Include uncommitted work too, and label it as uncommitted.

Read the commit messages, then open new or heavily changed scripts. Don't guess from file names when you can read the file.

No shell? Ask me what changed and work from my answer.

### 2. Update only what the changes touch

| What changed | Update |
|---|---|
| A system was finished | `03 Done/(C) Shipped Systems.md` |
| Scripts added, renamed or removed | `04 System/Codebase/(C) Codebase Map.md` |
| A decision was made (picked an approach, cut scope, reordered work) | add a row to `04 System/(C) Decision Log.md` |
| A bug found or fixed, or playtest feedback | `02 Playtesting/(C) Known Bugs and Playtest Plan.md` |
| Progress on the active update | the scope note in `01 In Progress/` (tick the box) |
| A note's stage changed | move it to the right numbered folder and fix links to it |

Every time:
- Add an entry to `07 Iteration Logs/(C) Session History.md`: date, a short title, 2 to 4 lines on what changed, and the commit hashes.
- Update **Current Status** in `CLAUDE.md`: the date, what changed, the next playtest date and next tasks, and **Last documented commit** (current `HEAD`).

### 3. Report back

Three lines at most: what you updated, the next task, and anything uncommitted I should commit.

## Rules

- **Facts only.** Write what the code and commits show, not what was planned. If something hasn't been tried in-game, say so.
- **Short.** A wrap should take a minute to read.
- **Ask before editing** any file without the `(C)` prefix, except the Current Status and Weekly Update sections of `CLAUDE.md`.
- **Don't commit** unless I ask.
