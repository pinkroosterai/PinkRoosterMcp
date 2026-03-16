---
name: pm-codesmells
description: >-
  Scan the codebase for common code smells using threshold-based detection:
  long methods, god classes, long parameter lists, deep nesting, magic
  numbers, data clumps, and more. Produces a prioritized overview and
  proposes PinkRooster issues for confirmed findings. Faster and more
  systematic than pm-audit — uses quantitative metrics rather than
  exploratory analysis. Use this skill whenever the user says "code smells",
  "find smells", "check for code smells", "method too long", "god class",
  "refactoring opportunities", "what needs refactoring", "code quality
  metrics", "complexity check", or wants a quick structural health scan
  of their code without a full audit.
argument-hint: "[--scope path/to/dir] [--threshold strict|normal|relaxed]"
---

# Code Smell Detection

Systematically scan the codebase for named code smell patterns using quantitative
thresholds and symbolic analysis. Unlike `/pm-audit` (which runs broad exploratory
agents across quality, security, performance, and architecture), this skill does
fast, targeted detection of specific structural patterns that indicate refactoring
opportunities.

The output is a prioritized list of confirmed code smells with proposed issues.
Each smell maps to a well-known refactoring pattern, giving developers clear
direction on how to fix it.

## Step 0: Parse Arguments

Parse `$ARGUMENTS` for flags:

- **`--scope <path>`**: Limit analysis to a directory (e.g., `src/PinkRooster.Api`).
  Default: entire `src/` directory (skips tests, docs, configs).
- **`--threshold <level>`**: Detection sensitivity.
  - `strict`: Lower thresholds — catches more, may have noise
  - `normal` (default): Balanced thresholds
  - `relaxed`: Higher thresholds — only the worst offenders

### Threshold Table

| Smell | Strict | Normal | Relaxed |
|-------|--------|--------|---------|
| Long Method (lines) | >30 | >50 | >80 |
| Large Class (members) | >15 | >25 | >40 |
| Large File (lines) | >300 | >500 | >800 |
| Long Parameter List | >4 | >5 | >7 |
| Deep Nesting (levels) | >3 | >4 | >5 |
| Magic Numbers (per file) | >3 | >5 | >10 |

## Step 1: Resolve Project and Check Existing Issues

- Current directory: /home/najgeetsrev/Development/PinkRoosterMcp
- Call `mcp__pinkrooster__get_project_status` with `projectPath` set to the directory above
- Extract the `projectId`

Load existing issues for deduplication:
1. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Active"`
2. `mcp__pinkrooster__get_issue_overview` with `projectId` and `stateFilter: "Inactive"`

## Step 2: Fast Structural Scan

Run these detection passes in parallel where possible. Each pass targets one smell
category using the most efficient tool for that pattern — Grep for textual patterns,
Bash for line counts, Serena for symbolic analysis.

The goal of this phase is speed: cast a wide net, then verify in Step 3.

### Pass 1: Large Files

Find files exceeding the line threshold. This is the cheapest scan and immediately
identifies candidates for deeper analysis.

```bash
find {scope} -name '*.cs' -o -name '*.ts' -o -name '*.tsx' | \
  xargs wc -l | sort -rn | head -30
```

Flag files above the Large File threshold.

### Pass 2: Long Methods / Functions

Use Serena's `mcp__serena__get_symbols_overview` on each large file (from Pass 1)
to get method-level line counts. For files not flagged as large, do a quick grep
for long method bodies.

For C# files, the symbol overview gives method locations (start/end lines) — compute
the span. For TypeScript files, grep for function/arrow function definitions and
estimate body length from the next closing brace at the same indent level.

Flag methods exceeding the Long Method threshold.

### Pass 3: Long Parameter Lists

Search for function/method signatures with many parameters:

```
# C# — methods with many params
Grep: pattern='\([^)]*,[^)]*,[^)]*,[^)]*,[^)]*,' glob='*.cs'

# TypeScript — functions/arrow functions with many params
Grep: pattern='\([^)]*,[^)]*,[^)]*,[^)]*,[^)]*,' glob='*.{ts,tsx}'
```

For each match, count the actual comma-separated parameters (the grep is a rough
filter — verify the count is above threshold).

### Pass 4: Deep Nesting

Search for deeply indented code, which indicates nested conditionals, loops, or
callbacks:

```
# C# — lines with 5+ levels of indentation (20+ spaces or 5+ tabs)
Grep: pattern='^\s{20,}\S' glob='*.cs'

# TypeScript — lines with deep indentation
Grep: pattern='^\s{10,}\S' glob='*.{ts,tsx}'
```

Group consecutive deeply-indented lines to identify the enclosing method.

### Pass 5: God Classes (Large Classes)

Use Serena's `mcp__serena__get_symbols_overview` with `depth: 1` on files identified
in Pass 1 as large. Count the number of direct children (methods + properties) of
each class. Flag classes exceeding the Large Class threshold.

Also look for classes with many injected dependencies (constructor parameters >5),
which signal a class doing too many things.

### Pass 6: Magic Numbers and Strings

Search for hardcoded numeric literals in logic code (not constants, not array
indices, not obvious values like 0, 1, -1):

```
Grep: pattern='[^a-zA-Z0-9_]([\d]{2,})[^a-zA-Z0-9_]' glob='*.cs'
```

Exclude:
- Constant/static readonly declarations (these ARE the named alternative)
- String interpolation index formatting
- Test files (magic numbers in tests are fine)
- Common values: 0, 1, -1, 2, 100, 1000 (often clear in context)

Flag files with more than the threshold number of magic numbers.

### Pass 7: Duplicate Method Signatures

Search for methods with identical names across different service classes, which
may indicate duplicated logic that should be consolidated:

```
Grep: pattern='public.*\s(\w+Async?)\(' glob='*.cs' path='src/'
```

Group by method name — if the same method name appears in 3+ files, it's a
candidate for extraction into a shared helper.

## Step 3: Verify and Classify Findings

Raw scan results will contain false positives. For each candidate finding:

1. **Read the actual code** at the flagged location (use Read tool with offset/limit
   to read just the relevant section, not the whole file)
2. **Apply judgment** — is this genuinely a code smell, or is the length/complexity
   justified by the problem being solved? Some methods are legitimately long because
   they handle a complex mapping or query. Mark these as false positives.
3. **Classify by smell name** using the catalog below
4. **Assign severity** based on how far above threshold the metric is:
   - Minor: 1-1.5x threshold (e.g., 55-line method when threshold is 50)
   - Major: 1.5-2x threshold (e.g., 80-line method)
   - Critical: >2x threshold (e.g., 120-line method)

### Code Smell Catalog

| Smell | Detection | Refactoring |
|-------|-----------|-------------|
| **Long Method** | Method body exceeds line threshold | Extract Method, Decompose Conditional |
| **God Class** | Class has too many members or dependencies | Extract Class, Move Method |
| **Large File** | File exceeds line threshold | Split into focused modules |
| **Long Parameter List** | Method has too many parameters | Introduce Parameter Object, Preserve Whole Object |
| **Deep Nesting** | Code nested beyond threshold | Extract Method, Replace Nested Conditional with Guard Clauses, Early Return |
| **Magic Numbers** | Hardcoded literals in logic | Extract Constant, Introduce Named Constant |
| **Data Clump** | Same group of parameters appears in multiple methods | Introduce Parameter Object |
| **Duplicate Logic** | Same method name/signature across classes | Extract to shared service or helper |
| **Feature Envy** | Method uses another class's data more than its own | Move Method to the class it envies |
| **Primitive Obsession** | Methods with many string/int params instead of typed objects | Introduce Value Object |

## Step 4: Present Findings

Sort findings by severity (Critical first), then by smell category.

```
## Code Smell Scan Results

**Scope**: {scope or "src/"}
**Threshold**: {level} ({description})
**Findings**: {count} across {categoryCount} smell categories
**Files scanned**: {fileCount}

### Critical ({count})
| # | Smell | Location | Metric | Threshold | Refactoring |
|---|-------|----------|--------|-----------|-------------|
| 1 | Long Method | `ProjectService.cs:GetNextActionsAsync` | 156 lines | >50 | Extract Method |

### Major ({count})
| # | Smell | Location | Metric | Threshold | Refactoring |
|---|-------|----------|--------|-----------|-------------|

### Minor ({count})
...

### False Positives Excluded ({count})
- `StateCascadeService.PropagateStateUpwardAsync` (172 lines) — complex state machine, legitimate length
```

Use `AskUserQuestion`:
- Question: "Which findings should I create as tracked issues?"
- Header: "Create issues"
- multiSelect: true
- Options:
  - "All Critical + Major ({N})" (recommended)
  - "All findings ({N})"
  - "Let me pick individually"
  - "None — just keep the report"

## Step 5: Create Issues

For each confirmed finding, call `mcp__pinkrooster__create_or_update_issue`:

- `projectId`
- `name`: `"Code smell: {smell name} in {class/method name}"`
- `description`: Include the smell name, metric value vs threshold, affected code
  location, and the recommended refactoring pattern. Reference the original code
  smell definition so the implementer understands the "why".
- `issueType`: `TechnicalDebt`
- `severity`: Mapped from smell severity (Critical/Major/Minor)
- `priority`: Critical→High, Major→Medium, Minor→Low
- `affectedComponent`: Derived from file path

When multiple smells affect the same class (e.g., God Class + Long Methods within it),
consolidate into a single issue that addresses the root cause (the God Class) rather
than creating separate issues for each method.

## Step 6: Report

```
## Created {count} Issue(s) from Code Smell Scan

| # | Issue ID | Smell | Location | Severity |
|---|----------|-------|----------|----------|
| 1 | {issueId} | {smell} | {location} | {severity} |

### Smell Distribution
- Long Method: {count}
- God Class: {count}
- ...

### Next Steps
- Scaffold a fix: `/pm-scaffold {issueId}`
- Full audit (security, performance, architecture): `/pm-audit`
- Hygiene cleanup (dead code, unused imports): `/pm-cleanup`
- View project status: `/pm-status`
```

## Constraints

- **Speed over depth**: This skill should complete in under 2 minutes for a
  medium-sized project. Use targeted searches, not full file reads. Only read
  code sections that were flagged by the scan passes.
- **Named smells only**: Every finding must map to a recognized code smell from
  the catalog. Generic complaints ("this code is messy") are not code smells.
- **Threshold-based**: Findings must cite a specific metric and its threshold.
  No subjective assessments — the numbers speak for themselves.
- **Verify before reporting**: Read the flagged code for Critical and Major findings
  to confirm they're genuine. Pass 2-7 produce candidates, not confirmed smells.
- **Consolidate related smells**: A God Class with 5 Long Methods is one issue
  (refactor the class), not 6 separate issues.
- **Deduplicate against existing issues**: Check PinkRooster before creating.
- **Maximum 8 issues per scan**: If more findings exist, report the top 8 by
  severity and note the remainder.
- **Respect scope**: Only analyze files within `--scope` when specified.
- **Skip test files**: Test code has different quality standards — long test methods
  and magic numbers are normal.
- **Skip generated files**: Migrations, auto-generated code, and vendor files should
  not be flagged.
