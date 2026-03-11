---
name: pm-verify
description: >-
  Verify acceptance criteria for a phase or entire work package. Runs
  verification based on each criterion's method (AutomatedTest, Manual,
  AgentReview) and records results via the MCP tool.
argument-hint: "<phase-id | wp-id> [--dry-run]"
---

# Verify Acceptance Criteria

Verify that acceptance criteria for a phase or work package are met. Records verification results
and timestamps. Does NOT change entity states — use `/pm-done` to complete phases after verification.

## Step 0: Parse Arguments

Parse `!arguments` for:

- **Target ID**: phase ID (e.g., `proj-1-wp-2-phase-1`) or WP ID (e.g., `proj-1-wp-2`)
- **`--dry-run`**: If present, show what would be verified without recording results

If no arguments provided, prompt: "Usage: `/pm-verify <phase-id | wp-id> [--dry-run]`"

## Step 1: Load Context

Call `mcp__pinkrooster__get_work_package_details` with the WP ID (extract from phase ID if needed).

Extract:
- All phases with their acceptance criteria
- If a phase ID was given, focus on that phase only
- If a WP ID was given, verify all phases with unverified criteria

Build a verification queue: list all acceptance criteria that have `verifiedAt == null` or need re-verification.

## Step 2: Verify Each Criterion

For each criterion in the queue, run verification based on `verificationMethod`:

### AutomatedTest
1. Check if the related tests exist (use Grep/Glob to find test files)
2. Run the relevant test suite:
   - `.NET tests`: `dotnet test` with appropriate filter
   - `Dashboard tests`: `cd src/dashboard && npm test`
3. Parse test output for pass/fail
4. Record result: "PASS: All N tests passed" or "FAIL: N/M tests failed — {details}"

### Manual
1. Read the target files associated with tasks in the same phase
2. Review the code against the criterion's description
3. Check that the described behavior or structure exists
4. Record result: "PASS: {evidence}" or "FAIL: {what's missing}"

### AgentReview
1. Analyze the code related to the criterion
2. Assess against the criterion's description using code understanding
3. Record result: "PASS: {assessment}" or "FAIL: {assessment}"

## Step 3: Record Results (unless --dry-run)

For each phase with verified criteria, call `mcp__pinkrooster__verify_acceptance_criteria` with:
- `phaseId`: the phase ID
- `criteria`: array of `[{"Name": "...", "VerificationResult": "..."}]`

If `--dry-run`, skip this step and show what would be recorded.

## Step 4: Report

```
## Verification Report — {targetId}

| Phase | Criterion | Method | Result |
|-------|-----------|--------|--------|
| {phaseName} | {criterionName} | {method} | PASS/FAIL |
| ... |

### Summary
- **Total criteria**: {count}
- **Passed**: {passCount}
- **Failed**: {failCount}
- **Skipped**: {skipCount} (if any)

### Failed Criteria
- **{criterionName}** ({phaseName}): {failureReason}
- ...

### Next Steps
- All passed? → Complete the work: `/pm-done {phaseId or wpId}`
- Failures? → Fix failing criteria: `/pm-implement {relevant-task-or-phase-id}`, then re-verify: `/pm-verify {targetId}`
- Check project progress: `/pm-status`
```

If `--dry-run`:
"Dry run complete. {count} criteria would be verified. Run without --dry-run to record results."

## Constraints

- This skill does NOT change entity states — it only records verification data
- Re-verification is safe (idempotent) — new results overwrite old ones
- For AutomatedTest criteria, always run the actual tests — don't just check if test files exist
- For Manual and AgentReview criteria, provide specific evidence from the code
- If a criterion name doesn't match any criterion on the phase, report an error
- Always show the full report even if all criteria pass
