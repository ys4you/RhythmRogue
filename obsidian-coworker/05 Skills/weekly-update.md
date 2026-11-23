# Skill: Weekly Update

Interview me and update this vault so Claude always has accurate, current context on RhythmRogue.

## When to Use

- I say "weekly update", "weekly review", "update the vault", or similar
- It's been a week or more since the last update (check "Last updated" in the Weekly Update section of `CLAUDE.md`)

## How It Works

1. Read the current state
2. Interview me (weekly pulse, goal check-in, project status)
3. Update the files
4. Optionally write a weekly review note

---

## Phase 1: Read Current State

Read before asking a single question:

- `CLAUDE.md`: the **Weekly Update**, **Goal & Current Progress**, and **Current Status** sections
- The notes in `01 In Progress/`: what is active and what its scope anchor says
- The newest note in `07 Iteration Logs/`
- If you have a shell: recent commits from the repo root (`git log --oneline -15`), so you can ask about real changes instead of making me list them

**What you're building:** a picture of what was true last time, so you only ask about what changed.

---

## Phase 2: Interview

Reference what you read so I know you're caught up. Keep it tight.

### Weekly Pulse

Show what the Weekly Update section says now (or that it's blank), then ask:

- **What's working right now?**
- **What's not working?**
- **What are you sitting on or need to decide?**
- **What are you feeling pulled toward?**
- **Any deadlines or time-sensitive things coming up?**

If a field hasn't changed, I can say "same" and you keep what was there.

### Goal Check-In

- **Any progress toward the Steam release since last time?** (milestones, playtests, setbacks)
- **Has the plan changed at all?**
- **Anything new on the risks side?**

If nothing changed, move on. Don't make me re-justify the goal every week.

### Project Status

Show the current **Current Status** from `CLAUDE.md` and the active `01 In Progress` note, then ask:

- **What moved this week?**
- **Does anything need to move between `00 Ideas`, `01 In Progress`, `02 Playtesting`, and `03 Done`?**

---

## Phase 3: Update Files

Show me a short summary of the changes before writing.

**`CLAUDE.md`:**
- **Weekly Update:** all five fields plus today's date
- **Goal & Current Progress:** only if something actually changed
- **Current Status:** new date, status, and next task

```markdown
## Current Status

> **Last updated:** [today's date]
> **Status:** [updated status from interview]

**Next task:** [what's next]
```

**Stage notes:** move or update notes whose stage changed. Ask before editing any file without the `(C)` prefix.

**Critical rule:** only touch status and progress. Never rewrite the structure, process, or rules in `CLAUDE.md` during a weekly update.

---

## Phase 4: Weekly Review Note (Optional)

Ask: "Want a weekly review note in `07 Iteration Logs`?"

- **Yes:** create `(C) Weekly Review - [date].md` from the interview answers. If an earlier weekly review exists, match its format.
- **No:** skip it. Don't push.

---

## Summary of What Gets Updated

| File | What changes |
|------|-------------|
| `CLAUDE.md` > Weekly Update | All five pulse fields plus date |
| `CLAUDE.md` > Goal & Current Progress | Only if the goal, plan, or risks changed |
| `CLAUDE.md` > Current Status | Status, date, next task |
| Stage notes | Moved or updated if their stage changed |
| `07 Iteration Logs/` | Optional weekly review note |
