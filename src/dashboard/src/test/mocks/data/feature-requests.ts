import type { FeatureRequest } from "@/types";

export function createFeatureRequest(overrides?: Partial<FeatureRequest>): FeatureRequest {
  return {
    featureRequestId: "proj-1-fr-1",
    id: 1,
    featureRequestNumber: 1,
    projectId: "proj-1",
    name: "Dashboard Dark Mode",
    description: "Add dark mode support to the dashboard",
    category: "Feature",
    priority: "High",
    status: "Approved",
    businessValue: "Improves UX for night users",
    userStory: "As a user I want dark mode",
    requester: "product-team",
    acceptanceSummary: "Theme toggle works in header",
    startedAt: null,
    completedAt: null,
    resolvedAt: null,
    attachments: [],
    linkedWorkPackages: [],
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-02T00:00:00Z",
    ...overrides,
  };
}
