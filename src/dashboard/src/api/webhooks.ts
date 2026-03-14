import { apiFetch } from "./client";

export interface WebhookSubscription {
  id: number;
  projectId: number;
  url: string;
  isActive: boolean;
  eventFilters: { eventType: string; entityType?: string }[];
  lastDeliveredAt: string | null;
  lastFailedAt: string | null;
  consecutiveFailures: number;
  createdAt: string;
  updatedAt: string;
}

export interface WebhookDeliveryLog {
  id: number;
  webhookSubscriptionId: number;
  eventType: string;
  entityType: string;
  entityId: string;
  attemptNumber: number;
  httpStatusCode: number | null;
  durationMs: number;
  success: boolean;
  nextRetryAt: string | null;
  createdAt: string;
}

export function getWebhooks(projectId: number): Promise<WebhookSubscription[]> {
  return apiFetch(`/projects/${projectId}/webhooks`);
}

export function getWebhookDeliveries(
  projectId: number,
  subscriptionId: number,
  limit = 50,
): Promise<WebhookDeliveryLog[]> {
  return apiFetch(
    `/projects/${projectId}/webhooks/${subscriptionId}/deliveries?limit=${limit}`,
  );
}
