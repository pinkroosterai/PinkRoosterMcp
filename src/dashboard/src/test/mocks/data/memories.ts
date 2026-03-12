import type { ProjectMemoryListItem, ProjectMemory } from "@/types";

export function createMemoryListItem(
  overrides?: Partial<ProjectMemoryListItem>,
): ProjectMemoryListItem {
  return {
    memoryId: "proj-1-mem-1",
    name: "Architecture Decisions",
    tags: ["architecture", "decisions"],
    updatedAt: "2026-03-01T12:00:00Z",
    ...overrides,
  };
}

export function createMemory(overrides?: Partial<ProjectMemory>): ProjectMemory {
  return {
    memoryId: "proj-1-mem-1",
    projectId: "proj-1",
    memoryNumber: 1,
    name: "Architecture Decisions",
    content: "We use vertical slice architecture with shared DTOs.",
    tags: ["architecture", "decisions"],
    createdAt: "2026-03-01T10:00:00Z",
    updatedAt: "2026-03-01T12:00:00Z",
    wasMerged: false,
    ...overrides,
  };
}
