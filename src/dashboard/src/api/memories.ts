import { apiFetch } from "./client";
import type { ProjectMemoryListItem, ProjectMemory } from "@/types";

export function getMemories(
  projectId: number,
  namePattern?: string,
  tag?: string,
): Promise<ProjectMemoryListItem[]> {
  const params = new URLSearchParams();
  if (namePattern) params.set("namePattern", namePattern);
  if (tag) params.set("tag", tag);
  const qs = params.toString();
  return apiFetch<ProjectMemoryListItem[]>(
    `/projects/${projectId}/memories${qs ? `?${qs}` : ""}`,
  );
}

export function getMemory(
  projectId: number,
  memoryNumber: number,
): Promise<ProjectMemory> {
  return apiFetch<ProjectMemory>(`/projects/${projectId}/memories/${memoryNumber}`);
}

export function upsertMemory(
  projectId: number,
  data: { name: string; content: string; tags?: string[] },
): Promise<ProjectMemory> {
  return apiFetch<ProjectMemory>(`/projects/${projectId}/memories`, {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export function deleteMemory(
  projectId: number,
  memoryNumber: number,
): Promise<void> {
  return apiFetch(`/projects/${projectId}/memories/${memoryNumber}`, {
    method: "DELETE",
  });
}
