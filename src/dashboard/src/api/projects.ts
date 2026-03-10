import { apiFetch } from "./client";
import type { Project } from "@/types";

export function getProjects(): Promise<Project[]> {
  return apiFetch<Project[]>("/projects");
}

export function deleteProject(id: number): Promise<void> {
  return apiFetch(`/projects/${id}`, { method: "DELETE" });
}
