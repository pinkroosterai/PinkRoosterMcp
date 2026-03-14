---
name: pm-troubleshoot
description: >-
  Diagnose the root cause of a bug, error, crash, or unexpected behavior.
  Traces through code, logs, services, database state, and git history
  to find why something is broken. Researches error messages online for
  known issues. Produces a diagnosis with evidence and suggested fix.
  Use when the user reports a problem, error message, stack trace,
  test failure, or says things like "why is this happening",
  "this is broken", "I'm getting an error", "something's wrong with...",
  "debug this", or "figure out why...".
argument-hint: <symptom description or error message>
disable-model-invocation: false
---

# Troubleshoot: Root Cause Analysis

Given a symptom — an error message, unexpected behavior, crash, test failure, or vague "something's wrong" — systematically trace through the codebase, logs, services, and git history to find the root cause. The goal is a clear diagnosis the user can act on, not a guess.

## Step 1: Capture the Symptom

Parse `$ARGUMENTS` to understand what went wrong. Classify the symptom:

**Error message / stack trace**: Extract the key error, file path, line number, and exception type. This is the most actionable starting point.

**Unexpected behavior**: "X happens when it should do Y." Understand both the expected and actual behavior.

**Test failure**: Extract the test name, assertion that failed, expected vs actual values.

**Vague report**: "Something's wrong with the dashboard" or "the API is slow." Need to narrow down before investigating — ask one focused question using `AskUserQuestion`:
- Question: "Can you describe the specific symptom? For example: an error message, a page that doesn't load, a request that returns wrong data, or a test that fails?"
- Header: "What's happening?"
- Options: `[{label: "Error/crash", description: "I see an error message or stack trace"}, {label: "Wrong behavior", description: "Something works but does the wrong thing"}, {label: "Performance", description: "Something is slow or timing out"}, {label: "Test failure", description: "A test is failing"}]`

Record the symptom clearly — this is the hypothesis to prove or disprove.

## Step 2: Check the Live Environment

Before diving into code, check if the issue is environmental. These checks are fast and often reveal the problem immediately.

### 2a: Service Health

```bash
# Check if containers are running and healthy
docker compose ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}" 2>/dev/null
```

If services are down or unhealthy, that may be the root cause. Check container logs:

```bash
# Last 50 lines from each service
docker compose logs --tail=50 api 2>/dev/null
docker compose logs --tail=50 mcp 2>/dev/null
docker compose logs --tail=50 dashboard 2>/dev/null
docker compose logs --tail=50 postgres 2>/dev/null
```

### 2b: Database State

If the symptom involves data issues, missing records, or FK violations:

```bash
# Check database connectivity and basic state
docker compose exec -T postgres psql -U postgres -d pinkrooster -c "SELECT count(*) FROM projects;" 2>/dev/null
```

For specific entity issues, query the relevant table. Use the connection string from the environment.

### 2c: Recent Changes

Check what changed recently — the cause is often in the last few commits:

```bash
git log --oneline -10
git diff HEAD~3 --stat
```

If the symptom started after a specific change, `git diff HEAD~1` or `git log --oneline -5 -- {affected-file}` narrows it down.

**Skip environment checks when**: The user provides a clear error with file/line reference, or the issue is obviously in static code (type errors, logic bugs). Go straight to Step 3.

## Step 3: Trace Through Code

This is the core investigation. Start from the symptom and work backward to the root cause.

### 3a: Locate the Error Origin

**If error message with file/line**: Read that file directly, understand the context.

**If stack trace**: Trace from the top (where the error surfaced) down to the bottom (where it originated). Read the key frames.

**If behavioral bug**: Identify the code path that produces the wrong behavior:
1. Find the relevant endpoint/page/component using Grep
2. Trace the data flow: controller → service → database (API) or component → hook → API call (dashboard)
3. Read each layer to find where the behavior diverges from expectation

**If test failure**: Read the test, understand what it asserts, then trace into the code under test.

### 3b: Analyze the Suspect Code

Use Serena's tools for efficient code exploration:
- `mcp__serena__get_symbols_overview` to understand file structure without reading everything
- `mcp__serena__find_symbol` with `include_body=True` to read specific functions
- `mcp__serena__find_referencing_symbols` to trace callers and dependencies

Look for common root cause patterns:
- **Null/undefined access**: Missing guard on optional data (check if API returns null vs empty array)
- **State mismatch**: Entity in wrong state for the operation being attempted
- **Missing migration**: Schema changed but migration not applied
- **Race condition**: Concurrent access without proper locking
- **Configuration**: Missing or wrong env var, connection string, API key
- **Dependency version**: Package conflict or breaking change after upgrade
- **Off-by-one / boundary**: Edge case in pagination, array indexing, date ranges

### 3c: Git Blame

If you've identified the suspect code but don't know when/why it was introduced:

```bash
git log --oneline -5 -- {suspect-file}
git blame -L {startLine},{endLine} {suspect-file}
```

This reveals who changed the code and what the commit message says — often explains the intent behind the bug.

## Step 4: Research Online

For error messages, library issues, or patterns you're unsure about — search online. This catches known issues, version-specific bugs, and correct usage patterns that aren't obvious from code alone.

**When to research:**
- The error message includes a library name or framework-specific code (e.g., "Npgsql.PostgresException", "ERR_CONNECTION_REFUSED", "CORS policy")
- The suspect code uses an API or pattern you want to verify is correct
- The issue might be a known bug in a dependency

**How to research:**
- Use `WebSearch` with the exact error message (in quotes) plus the technology stack
- Use `WebSearch` with the library name + "correct usage" for pattern verification
- Use `WebFetch` to pull relevant documentation or GitHub issues

**When to skip:**
- The root cause is clearly a logic error in project code (typo, wrong variable, missing guard)
- The issue is a data problem (wrong value in DB, missing record)

## Step 5: Formulate Diagnosis

Synthesize findings into a clear diagnosis. The quality of the diagnosis determines whether the fix will be correct on the first attempt.

### Build the Evidence Chain

Trace from symptom → immediate cause → root cause:

```
Symptom: Dashboard shows blank white screen on WP detail page
  ↓
Immediate cause: `phases.map()` throws because `phases` is undefined
  ↓
Root cause: API returns `null` for phases when WP has none;
            TypeScript type says `Phase[]` (non-optional) but API doesn't guarantee it
  ↓
Fix: Add null coalescing in the component OR fix the API to return `[]` instead of `null`
```

### Assess Confidence

Rate your diagnosis:
- **High confidence**: Reproduced the issue, found the exact line, understand why it happens
- **Medium confidence**: Found the likely cause but couldn't fully reproduce, or there are multiple possible causes
- **Low confidence**: Narrowed the area but root cause is still uncertain — more investigation needed

## Step 6: Report

```
## Diagnosis: {one-line summary}

### Symptom
{What the user reported or observed}

### Root Cause
{Clear explanation of WHY the issue occurs — not just WHERE}

### Evidence
1. {File:line — what was found and what it means}
2. {Log entry / DB state / git change — supporting evidence}
3. {Online reference — if research confirmed the diagnosis}

### Evidence Chain
{symptom} → {immediate cause} → {root cause}

### Confidence: {High | Medium | Low}
{Brief justification for the confidence level}

### Suggested Fix
{Specific code change or configuration fix}
- File: {path}
- Change: {what to do}

### Prevention
{How to prevent this class of issue in the future — optional, only if there's a clear pattern}

### Research (if performed)
- {key finding from online research}
```

## Step 7: Offer Next Steps

Use the `AskUserQuestion` tool:
- Question: "How would you like to proceed with this diagnosis?"
- Header: "Next step"
- Options: `[{label: "Create issue", description: "Track this as a bug via /pm-plan with the root cause pre-filled"}, {label: "Fix now", description: "Implement the fix directly"}, {label: "Investigate more", description: "The diagnosis isn't conclusive — dig deeper"}, {label: "Done", description: "I have what I need"}]`

**If Create issue**: Invoke `/pm-plan` with a pre-formatted description:
```
Bug: {symptom summary}. Root cause: {root cause}. Fix: {suggested fix}.
Affected: {file paths}. Severity: {inferred from impact}.
```

**If Fix now**: Invoke `/pm-implement` if a task exists, or implement the fix directly following the suggested change.

**If Investigate more**: Ask what aspect needs more investigation and continue from Step 3.

## Constraints

- Always start with the symptom, not assumptions — let the evidence guide the investigation
- Check the live environment before diving into code — environmental issues are the most common and fastest to diagnose
- Follow the evidence chain: symptom → immediate cause → root cause. Don't stop at the immediate cause (e.g., "phases is undefined" is not the root cause — WHY is it undefined is)
- Rate your confidence honestly — a "medium confidence" diagnosis that says so is more useful than a wrong "high confidence" one
- Research online when the issue involves libraries, frameworks, or patterns — don't rely on stale knowledge when docs are a search away
- Keep the investigation proportional — a clear null pointer error doesn't need 10 minutes of git archaeology
- When multiple root causes are possible, present all of them ranked by likelihood rather than guessing at one
- The diagnosis should be actionable — if the user can't act on it, dig deeper
