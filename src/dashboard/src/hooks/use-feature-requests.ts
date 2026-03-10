import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getFeatureRequests,
  getFeatureRequest,
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
