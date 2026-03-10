import { apiFetch } from "./client";
import type { ActivityLog, PaginatedResponse } from "@/types";

export function getActivityLogs(
  page = 1,
  pageSize = 25,
): Promise<PaginatedResponse<ActivityLog>> {
  return apiFetch(`/activity-logs?page=${page}&pageSize=${pageSize}`);
}
