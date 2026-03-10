import { apiFetch } from "./client";
import type { WorkPackage, WorkPackageSummary } from "@/types";

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
