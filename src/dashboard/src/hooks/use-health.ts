import { useQuery } from "@tanstack/react-query";

async function checkHealth(): Promise<boolean> {
  try {
    const res = await fetch("/api/activity-logs?page=1&pageSize=1");
    return res.ok;
  } catch {
    return false;
  }
}

export function useHealth() {
  return useQuery({
    queryKey: ["health"],
    queryFn: checkHealth,
    refetchInterval: 30_000,
  });
}
