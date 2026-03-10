import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getWorkPackages,
  getWorkPackage,
  getWorkPackageSummary,
  deleteWorkPackage,
  deletePhase,
  deleteTask,
} from "@/api/work-packages";

export function useWorkPackages(projectId: number | undefined, stateFilter?: string) {
  return useQuery({
    queryKey: ["work-packages", projectId, stateFilter],
    queryFn: () => getWorkPackages(projectId!, stateFilter),
    enabled: projectId !== undefined,
  });
}

export function useWorkPackage(projectId: number, wpNumber: number) {
  return useQuery({
    queryKey: ["work-package", projectId, wpNumber],
    queryFn: () => getWorkPackage(projectId, wpNumber),
  });
}

export function useWorkPackageSummary(projectId: number | undefined) {
  return useQuery({
    queryKey: ["work-package-summary", projectId],
    queryFn: () => getWorkPackageSummary(projectId!),
    enabled: projectId !== undefined,
  });
}

export function useDeleteWorkPackage() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, wpNumber }: { projectId: number; wpNumber: number }) =>
      deleteWorkPackage(projectId, wpNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["work-packages"] });
      queryClient.invalidateQueries({ queryKey: ["work-package-summary"] });
    },
  });
}

export function useDeletePhase() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      projectId,
      wpNumber,
      phaseNumber,
    }: {
      projectId: number;
      wpNumber: number;
      phaseNumber: number;
    }) => deletePhase(projectId, wpNumber, phaseNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["work-package"] });
    },
  });
}

export function useDeleteTask() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      projectId,
      wpNumber,
      taskNumber,
    }: {
      projectId: number;
      wpNumber: number;
      taskNumber: number;
    }) => deleteTask(projectId, wpNumber, taskNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["work-package"] });
    },
  });
}
