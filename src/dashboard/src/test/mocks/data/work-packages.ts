import type { WorkPackage, Phase, WpTask, WorkPackageSummary } from "@/types";

export function createTask(overrides?: Partial<WpTask>): WpTask {
  return {
    taskId: "proj-1-wp-1-task-1",
    id: 1,
    taskNumber: 1,
    phaseId: "proj-1-wp-1-phase-1",
    name: "Implement feature",
    description: "Write the code",
    sortOrder: 1,
    implementationNotes: null,
    state: "NotStarted",
    previousActiveState: null,
    startedAt: null,
    completedAt: null,
    resolvedAt: null,
    targetFiles: [],
    attachments: [],
    blockedBy: [],
    blocking: [],
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

export function createPhase(overrides?: Partial<Phase>): Phase {
  return {
    phaseId: "proj-1-wp-1-phase-1",
    id: 1,
    phaseNumber: 1,
    name: "Implementation",
    description: "Build the feature",
    sortOrder: 1,
    state: "NotStarted",
    tasks: [createTask()],
    acceptanceCriteria: [],
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

export function createWorkPackage(overrides?: Partial<WorkPackage>): WorkPackage {
  return {
    workPackageId: "proj-1-wp-1",
    id: 1,
    workPackageNumber: 1,
    projectId: "proj-1",
    name: "Test Work Package",
    description: "A work package for testing",
    type: "Feature",
    priority: "High",
    plan: null,
    estimatedComplexity: null,
    estimationRationale: null,
    state: "NotStarted",
    previousActiveState: null,
    linkedIssueId: null,
    linkedFeatureRequestId: null,
    startedAt: null,
    completedAt: null,
    resolvedAt: null,
    attachments: [],
    phases: [createPhase()],
    blockedBy: [],
    blocking: [],
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

export function createWorkPackageSummary(overrides?: Partial<WorkPackageSummary>): WorkPackageSummary {
  return {
    activeCount: 3,
    inactiveCount: 2,
    terminalCount: 1,
    ...overrides,
  };
}
