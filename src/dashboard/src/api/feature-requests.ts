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

export function deleteFeatureRequest(
  projectId: number,
  frNumber: number,
): Promise<void> {
  return apiFetch(`/projects/${projectId}/feature-requests/${frNumber}`, {
    method: "DELETE",
  });
}
