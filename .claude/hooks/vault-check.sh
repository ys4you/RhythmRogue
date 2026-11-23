#!/usr/bin/env bash
# SessionStart hook: tells Claude when the vault's Current Status is behind git,
# so it offers the session-wrap skill before new work. Prints nothing when up to date.
cd "$(dirname "$0")/../.." || exit 0
vault="obsidian-coworker/CLAUDE.md"
[ -f "$vault" ] || exit 0

last=$(grep -m1 -o 'Last documented commit:\*\* `[0-9a-f]*`' "$vault" | grep -o '[0-9a-f]\{7,\}')
[ -n "$last" ] || exit 0

if ! behind=$(git rev-list --count "$last"..HEAD 2>/dev/null); then
  echo "Vault check: the last documented commit ($last) in $vault is not in git history. Ask Yesse what changed, or run the session-wrap skill from the last commit you can find."
  exit 0
fi
dirty=$(git status --porcelain -- Assets Packages ProjectSettings 2>/dev/null | wc -l | tr -d ' ')

if [ "$behind" -gt 0 ] || [ "$dirty" -gt 0 ]; then
  echo "Vault check: $vault was last documented at commit $last. Since then: $behind new commit(s) and $dirty uncommitted change(s) in Assets, Packages or ProjectSettings. Offer to run the session-wrap skill (obsidian-coworker/05 Skills/session-wrap.md) before starting new work."
fi
exit 0
