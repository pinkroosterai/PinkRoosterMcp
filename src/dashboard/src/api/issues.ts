import { apiFetch } from "./client";
import type { Issue, IssueSummary, IssueAuditLog } from "@/types";

export function getIssues(
  projectId: number,
  state?: string,
): Promise<Issue[]> {
  const params = state ? `?state=${encodeURIComponent(state)}` : "";
  return apiFetch<Issue[]>(`/projects/${projectId}/issues${params}`);
}

export function getIssue(
  projectId: number,
  issueNumber: number,
): Promise<Issue> {
  return apiFetch<Issue>(`/projects/${projectId}/issues/${issueNumber}`);
}

export function getIssueSummary(projectId: number): Promise<IssueSummary> {
  return apiFetch<IssueSummary>(`/projects/${projectId}/issues/summary`);
}

export function getIssueAuditLog(
  projectId: number,
  issueNumber: number,
): Promise<IssueAuditLog[]> {
  return apiFetch<IssueAuditLog[]>(
    `/projects/${projectId}/issues/${issueNumber}/audit`,
  );
}

export function deleteIssue(
  projectId: number,
  issueNumber: number,
): Promise<void> {
  return apiFetch(`/projects/${projectId}/issues/${issueNumber}`, {
    method: "DELETE",
  });
}
