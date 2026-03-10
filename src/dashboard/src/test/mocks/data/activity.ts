import type { ActivityLog, PaginatedResponse } from "@/types";

export function createActivityLog(overrides?: Partial<ActivityLog>): ActivityLog {
  return {
    id: 1,
    httpMethod: "GET",
    path: "/api/projects",
    statusCode: 200,
    durationMs: 42,
    callerIdentity: "mcp-agent",
    timestamp: "2026-01-01T12:00:00Z",
    ...overrides,
  };
}

export function createPaginatedLogs(
  items: ActivityLog[] = [createActivityLog()],
  overrides?: Partial<PaginatedResponse<ActivityLog>>,
): PaginatedResponse<ActivityLog> {
  return {
    items,
    page: 1,
    pageSize: 25,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
    ...overrides,
  };
}
