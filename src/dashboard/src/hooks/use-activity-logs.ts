import { useQuery } from "@tanstack/react-query";
import { getActivityLogs } from "@/api/activity";

export function useActivityLogs(page = 1, pageSize = 25) {
  return useQuery({
    queryKey: ["activity-logs", page, pageSize],
    queryFn: () => getActivityLogs(page, pageSize),
  });
}
