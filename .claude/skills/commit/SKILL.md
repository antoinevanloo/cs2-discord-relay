---
name: commit
description: "Use proactively when the user asks to commit, create a commit or save changes in git, or after finishing a task the user wants committed. Optional --push argument pushes right after the commit."
---

# Skill: create a commit following the repository conventions

Creates a Git commit for **cs2-discord-relay** using [Conventional Commits](https://www.conventionalcommits.org/).

## Message convention

Format: `<type>(<optional scope>): <description>`

- English, imperative mood, lower-case, no trailing period, subject ≤ 72 characters.
- Optional body (wrapped at 72 columns) explaining **why**, not what.
- Breaking change: `<type>!:` and a `BREAKING CHANGE:` footer.

### Types

| Type       | Use for                                             |
|------------|-----------------------------------------------------|
| `feat`     | New user-facing feature                             |
| `fix`      | Bug fix                                             |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf`     | Performance improvement                             |
| `test`     | Adding or fixing tests only                         |
| `docs`     | README, CONTRIBUTING, CHANGELOG, XML docs           |
| `ci`       | GitHub Actions workflows                            |
| `build`    | csproj, Directory.*.props, global.json, NuGet bumps |
| `chore`    | Repository maintenance (gitignore, editorconfig...) |

### Scopes

`core` (Cs2DiscordRelay.Core), `plugin` (Cs2DiscordRelay), `tests`, `release`. Omit when the change spans several areas.

## Procedure

### Step 1: analyse the changes

```bash
git status
git diff --staged
git diff
git log --oneline -10   # stay consistent with the history
```

### Step 2: pick type and scope

Derive them from the touched files:
- `src/Cs2DiscordRelay.Core/**` → scope `core`
- `src/Cs2DiscordRelay/**` → scope `plugin`
- `tests/**` only → `test`
- `.github/workflows/**` → `ci`
- `*.csproj`, `Directory.*.props`, `global.json` → `build`
- `*.md` only → `docs`

If the diff mixes unrelated concerns, propose splitting it into several atomic commits.

### Step 3: check before committing

```bash
dotnet build -c Release   # must report 0 warnings
dotnet test -c Release
```

Do not commit a red build unless the user explicitly asks for it.

### Step 4: stage files

**Never use `git add -A` or `git add .`.** Stage only the relevant files:

```bash
git add <file1> <file2> ...
```

Always exclude `bin/`, `obj/`, `*.zip`, logs, local configs and anything containing a webhook URL.

### Step 5: create the commit

```bash
git commit -m "$(cat <<'MSG'
<type>(<scope>): <description>

<optional body>
MSG
)"
```

### Step 6: ask about pushing

Use **AskUserQuestion**:
- **Question**: "Push to the remote branch?"
- **Options**: `["Yes, push", "No, commit only"]`

If `--push` was passed, push directly without asking.

## Example

```bash
git add src/Cs2DiscordRelay.Core/Discord/WebhookDispatcher.cs tests/Cs2DiscordRelay.Core.Tests/Discord/WebhookDispatcherTests.cs
git commit -m "$(cat <<'MSG'
fix(core): honour retry_after from the 429 body before the header

The header is rounded to whole seconds while the body is precise to the
millisecond, so bursts were waiting longer than needed.
MSG
)"
```

## Reminders

- **NEVER** mention AI in commit messages: no `Co-Authored-By`, no "Generated with" line.
- **ALWAYS** review the staged files before committing.
- **NEVER** commit without the user's explicit agreement.
- **NEVER** push without explicit agreement at that moment (unless `--push`).
- Git tooling: `gh` (GitHub).
