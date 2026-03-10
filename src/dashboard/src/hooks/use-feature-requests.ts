import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getFeatureRequests,
  getFeatureRequest,
  createFeatureRequest,
  updateFeatureRequest,
  deleteFeatureRequest,
} from "@/api/feature-requests";

export function useFeatureRequests(projectId: number | undefined, stateFilter?: string) {
  return useQuery({
    queryKey: ["feature-requests", projectId, stateFilter],
    queryFn: () => getFeatureRequests(projectId!, stateFilter),
    enabled: projectId !== undefined,
  });
}

export function useFeatureRequest(projectId: number, frNumber: number) {
  return useQuery({
    queryKey: ["feature-request", projectId, frNumber],
    queryFn: () => getFeatureRequest(projectId, frNumber),
  });
}

export function useCreateFeatureRequest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, data }: { projectId: number; data: Record<string, unknown> }) =>
      createFeatureRequest(projectId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["feature-requests"] });
    },
  });
}

export function useUpdateFeatureRequest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, frNumber, data }: { projectId: number; frNumber: number; data: Record<string, unknown> }) =>
      updateFeatureRequest(projectId, frNumber, data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["feature-request", variables.projectId, variables.frNumber] });
      queryClient.invalidateQueries({ queryKey: ["feature-requests"] });
    },
  });
}

export function useDeleteFeatureRequest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, frNumber }: { projectId: number; frNumber: number }) =>
      deleteFeatureRequest(projectId, frNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["feature-requests"] });
    },
  });
}
