import { apiFetch } from "./client";
import type { FeatureRequest } from "@/types";

export function getFeatureRequests(
  projectId: number,
  state?: string,
): Promise<FeatureRequest[]> {
  const params = state ? `?state=${encodeURIComponent(state)}` : "";
  return apiFetch<FeatureRequest[]>(`/projects/${projectId}/feature-requests${params}`);
}

export function getFeatureRequest(
  projectId: number,
  frNumber: number,
): Promise<FeatureRequest> {
  return apiFetch<FeatureRequest>(`/projects/${projectId}/feature-requests/${frNumber}`);
}

export function createFeatureRequest(
  projectId: number,
  data: Record<string, unknown>,
): Promise<FeatureRequest> {
  return apiFetch<FeatureRequest>(`/projects/${projectId}/feature-requests`, {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function updateFeatureRequest(
  projectId: number,
  frNumber: number,
  data: Record<string, unknown>,
): Promise<FeatureRequest> {
  return apiFetch<FeatureRequest>(`/projects/${projectId}/feature-requests/${frNumber}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
}

export function manageUserStories(
  projectId: number,
  frNumber: number,
  data: { action: string; index?: number; role?: string; goal?: string; benefit?: string },
): Promise<import("@/types").FeatureRequest> {
  return apiFetch<import("@/types").FeatureRequest>(
    `/projects/${projectId}/feature-requests/${frNumber}/user-stories/manage`,
    {
      method: "POST",
      body: JSON.stringify(data),
    },
  );
}

export function deleteFeatureRequest(
  projectId: number,
  frNumber: number,
): Promise<void> {
  return apiFetch(`/projects/${projectId}/feature-requests/${frNumber}`, {
    method: "DELETE",
  });
}
