import { apiFetch } from "./client";
import type { Project, ProjectStatus, NextActionItem } from "@/types";

export function getProjects(): Promise<Project[]> {
  return apiFetch<Project[]>("/projects");
}

export function deleteProject(id: number): Promise<void> {
  return apiFetch(`/projects/${id}`, { method: "DELETE" });
}

export function getProjectStatus(projectId: number): Promise<ProjectStatus> {
  return apiFetch<ProjectStatus>(`/projects/${projectId}/status`);
}

export function getNextActions(
  projectId: number,
  limit?: number,
  entityType?: string,
): Promise<NextActionItem[]> {
  const params = new URLSearchParams();
  if (limit) params.set("limit", String(limit));
  if (entityType) params.set("entityType", entityType);
  const qs = params.toString();
  return apiFetch<NextActionItem[]>(`/projects/${projectId}/next-actions${qs ? `?${qs}` : ""}`);
}
