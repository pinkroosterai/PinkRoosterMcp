import { apiFetch } from "./client";
import type { WorkPackage, WorkPackageSummary, WpTask } from "@/types";

export function getWorkPackages(
  projectId: number,
  state?: string,
): Promise<WorkPackage[]> {
  const params = state ? `?state=${encodeURIComponent(state)}` : "";
  return apiFetch<WorkPackage[]>(`/projects/${projectId}/work-packages${params}`);
}

export function getWorkPackage(
  projectId: number,
  wpNumber: number,
): Promise<WorkPackage> {
  return apiFetch<WorkPackage>(`/projects/${projectId}/work-packages/${wpNumber}`);
}

export function getWorkPackageSummary(
  projectId: number,
): Promise<WorkPackageSummary> {
  return apiFetch<WorkPackageSummary>(
    `/projects/${projectId}/work-packages/summary`,
  );
}

export function deleteWorkPackage(
  projectId: number,
  wpNumber: number,
): Promise<void> {
  return apiFetch(`/projects/${projectId}/work-packages/${wpNumber}`, {
    method: "DELETE",
  });
}

export function deletePhase(
  projectId: number,
  wpNumber: number,
  phaseNumber: number,
): Promise<void> {
  return apiFetch(
    `/projects/${projectId}/work-packages/${wpNumber}/phases/${phaseNumber}`,
    { method: "DELETE" },
  );
}

export function updateWorkPackage(
  projectId: number,
  wpNumber: number,
  data: Record<string, unknown>,
): Promise<WorkPackage> {
  return apiFetch<WorkPackage>(
    `/projects/${projectId}/work-packages/${wpNumber}`,
    { method: "PATCH", body: JSON.stringify(data) },
  );
}

export function updateTask(
  projectId: number,
  wpNumber: number,
  taskNumber: number,
  data: Record<string, unknown>,
): Promise<WpTask> {
  return apiFetch<WpTask>(
    `/projects/${projectId}/work-packages/${wpNumber}/tasks/${taskNumber}`,
    { method: "PATCH", body: JSON.stringify(data) },
  );
}

export function createWorkPackage(
  projectId: number,
  data: Record<string, unknown>,
): Promise<WorkPackage> {
  return apiFetch<WorkPackage>(`/projects/${projectId}/work-packages`, {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function deleteTask(
  projectId: number,
  wpNumber: number,
  taskNumber: number,
): Promise<void> {
  return apiFetch(
    `/projects/${projectId}/work-packages/${wpNumber}/tasks/${taskNumber}`,
    { method: "DELETE" },
  );
}
