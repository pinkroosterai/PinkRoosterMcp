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

export function createIssue(
  projectId: number,
  data: Record<string, unknown>,
): Promise<Issue> {
  return apiFetch<Issue>(`/projects/${projectId}/issues`, {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function updateIssue(
  projectId: number,
  issueNumber: number,
  data: Record<string, unknown>,
): Promise<Issue> {
  return apiFetch<Issue>(`/projects/${projectId}/issues/${issueNumber}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
}

export function deleteIssue(
  projectId: number,
  issueNumber: number,
): Promise<void> {
  return apiFetch(`/projects/${projectId}/issues/${issueNumber}`, {
    method: "DELETE",
  });
}
