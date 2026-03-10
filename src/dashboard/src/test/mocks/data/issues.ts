import type { Issue, IssueSummary, IssueAuditLog } from "@/types";

export function createIssue(overrides?: Partial<Issue>): Issue {
  return {
    issueId: "proj-1-issue-1",
    id: 1,
    issueNumber: 1,
    projectId: "proj-1",
    name: "Test Bug",
    description: "Something is broken",
    issueType: "Bug",
    severity: "Major",
    priority: "High",
    stepsToReproduce: "1. Open app\n2. Click button",
    expectedBehavior: "Should work",
    actualBehavior: "Does not work",
    affectedComponent: "Dashboard",
    stackTrace: null,
    rootCause: null,
    resolution: null,
    state: "Implementing",
    startedAt: "2026-01-02T00:00:00Z",
    completedAt: null,
    resolvedAt: null,
    attachments: [],
    linkedWorkPackages: [],
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-02T00:00:00Z",
    ...overrides,
  };
}

export function createIssueSummary(overrides?: Partial<IssueSummary>): IssueSummary {
  return {
    activeCount: 2,
    inactiveCount: 1,
    terminalCount: 3,
    latestTerminalIssues: [],
    ...overrides,
  };
}

export function createIssueAuditLog(overrides?: Partial<IssueAuditLog>): IssueAuditLog {
  return {
    fieldName: "State",
    oldValue: "NotStarted",
    newValue: "Implementing",
    changedBy: "mcp-agent",
    changedAt: "2026-01-02T00:00:00Z",
    ...overrides,
  };
}
