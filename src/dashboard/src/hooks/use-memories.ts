import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getMemories, getMemory, upsertMemory, deleteMemory } from "@/api/memories";

export function useMemories(projectId: number | undefined, namePattern?: string, tag?: string) {
  return useQuery({
    queryKey: ["memories", projectId, namePattern, tag],
    queryFn: () => getMemories(projectId!, namePattern, tag),
    enabled: projectId !== undefined,
  });
}

export function useMemory(projectId: number, memoryNumber: number) {
  return useQuery({
    queryKey: ["memory", projectId, memoryNumber],
    queryFn: () => getMemory(projectId, memoryNumber),
  });
}

export function useUpsertMemory() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      projectId,
      data,
    }: {
      projectId: number;
      data: { name: string; content: string; tags?: string[] };
    }) => upsertMemory(projectId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["memories"] });
      queryClient.invalidateQueries({ queryKey: ["project-status"] });
    },
  });
}

export function useDeleteMemory() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, memoryNumber }: { projectId: number; memoryNumber: number }) =>
      deleteMemory(projectId, memoryNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["memories"] });
      queryClient.invalidateQueries({ queryKey: ["project-status"] });
    },
  });
}
