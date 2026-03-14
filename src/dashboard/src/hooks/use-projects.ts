import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getProjects, deleteProject, getProjectStatus, getNextActions } from "@/api/projects";

export function useProjects() {
  return useQuery({
    queryKey: ["projects"],
    queryFn: getProjects,
    staleTime: 5_000,
    refetchOnWindowFocus: true,
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteProject,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useProjectStatus(projectId: number | undefined) {
  return useQuery({
    queryKey: ["project-status", projectId],
    queryFn: () => getProjectStatus(projectId!),
    enabled: projectId !== undefined,
  });
}

export function useNextActions(projectId: number | undefined, limit?: number, entityType?: string) {
  return useQuery({
    queryKey: ["next-actions", projectId, limit, entityType],
    queryFn: () => getNextActions(projectId!, limit, entityType),
    enabled: projectId !== undefined,
  });
}
