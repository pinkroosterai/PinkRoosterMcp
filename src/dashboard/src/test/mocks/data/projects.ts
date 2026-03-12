import type { Project, ProjectStatus, NextActionItem } from "@/types";

export function createProject(overrides?: Partial<Project>): Project {
  return {
    projectId: "proj-1",
    id: 1,
    name: "Test Project",
    description: "A test project",
    projectPath: "/home/user/test-project",
    status: "Active",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-02T00:00:00Z",
    ...overrides,
  };
}

export function createProjectStatus(overrides?: Partial<ProjectStatus>): ProjectStatus {
  return {
    projectId: "proj-1",
    name: "Test Project",
    status: "Active",
    issues: {
      total: 5,
      active: 2,
      inactive: 1,
      terminal: 2,
      percentComplete: 40,
      activeItems: [{ id: "proj-1-issue-1", name: "Active bug" }],
      inactiveItems: [{ id: "proj-1-issue-3", name: "Blocked issue" }],
    },
    featureRequests: {
      total: 3,
      active: 1,
      inactive: 1,
      terminal: 1,
      percentComplete: 33,
      activeItems: [{ id: "proj-1-fr-1", name: "Dark Mode" }],
      inactiveItems: [{ id: "proj-1-fr-2", name: "Deferred FR" }],
    },
    workPackages: {
      total: 8,
      terminalCount: 3,
      percentComplete: 37,
      active: [{ id: "proj-1-wp-1", name: "Active WP" }],
      inactive: [{ id: "proj-1-wp-2", name: "Not Started WP" }],
      blocked: [{ id: "proj-1-wp-3", name: "Blocked WP" }],
    },
    memories: null,
    ...overrides,
  };
}

export function createNextActionItem(overrides?: Partial<NextActionItem>): NextActionItem {
  return {
    type: "Task",
    id: "proj-1-wp-1-task-1",
    name: "Implement feature X",
    priority: "High",
    state: "Implementing",
    parentId: "proj-1-wp-1",
    ...overrides,
  };
}
