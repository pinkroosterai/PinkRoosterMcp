import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getIssues,
  getIssue,
  getIssueSummary,
  getIssueAuditLog,
  deleteIssue,
} from "@/api/issues";

export function useIssues(projectId: number | undefined, stateFilter?: string) {
  return useQuery({
    queryKey: ["issues", projectId, stateFilter],
    queryFn: () => getIssues(projectId!, stateFilter),
    enabled: projectId !== undefined,
  });
}

export function useIssue(projectId: number, issueNumber: number) {
  return useQuery({
    queryKey: ["issue", projectId, issueNumber],
    queryFn: () => getIssue(projectId, issueNumber),
  });
}

export function useIssueSummary(projectId: number | undefined) {
  return useQuery({
    queryKey: ["issue-summary", projectId],
    queryFn: () => getIssueSummary(projectId!),
    enabled: projectId !== undefined,
  });
}

export function useIssueAuditLog(projectId: number, issueNumber: number) {
  return useQuery({
    queryKey: ["issue-audit", projectId, issueNumber],
    queryFn: () => getIssueAuditLog(projectId, issueNumber),
  });
}

export function useDeleteIssue() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, issueNumber }: { projectId: number; issueNumber: number }) =>
      deleteIssue(projectId, issueNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["issues"] });
      queryClient.invalidateQueries({ queryKey: ["issue-summary"] });
    },
  });
}
