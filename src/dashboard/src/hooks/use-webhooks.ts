import { useQuery } from "@tanstack/react-query";
import { getWebhooks, getWebhookDeliveries } from "@/api/webhooks";

export function useWebhooks(projectId: number | undefined) {
  return useQuery({
    queryKey: ["webhooks", projectId],
    queryFn: () => getWebhooks(projectId!),
    enabled: !!projectId,
  });
}

export function useWebhookDeliveries(
  projectId: number | undefined,
  subscriptionId: number | undefined,
  limit = 50,
) {
  return useQuery({
    queryKey: ["webhook-deliveries", projectId, subscriptionId, limit],
    queryFn: () => getWebhookDeliveries(projectId!, subscriptionId!, limit),
    enabled: !!projectId && !!subscriptionId,
  });
}
