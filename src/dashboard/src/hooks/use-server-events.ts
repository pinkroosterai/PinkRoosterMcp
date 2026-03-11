import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

export type ConnectionState = "connected" | "reconnecting" | "disconnected";

interface ServerEvent {
  eventType: string;
  entityType: string;
  entityId: string;
  action: string;
  projectId: number;
  stateChanges?: { entityId: string; oldState: string; newState: string; reason: string }[];
}

const ENTITY_QUERY_KEYS: Record<string, string[][]> = {
  Issue: [["issues"], ["issue"], ["issue-summary"]],
  WorkPackage: [["work-packages"], ["work-package"], ["work-package-summary"]],
  Task: [["work-package"], ["work-packages"], ["work-package-summary"]],
  Phase: [["work-package"], ["work-packages"]],
  FeatureRequest: [["feature-requests"], ["feature-request"]],
};

const ALWAYS_INVALIDATE = [["project-status"], ["next-actions"]];

export function useServerEvents(projectId: number | undefined) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<ConnectionState>("disconnected");
  const eventSourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    if (!projectId) {
      setConnectionState("disconnected");
      return;
    }

    const url = `/api/projects/${projectId}/events`;
    const es = new EventSource(url);
    eventSourceRef.current = es;

    es.onopen = () => setConnectionState("connected");
    es.onerror = () => {
      if (es.readyState === EventSource.CONNECTING) {
        setConnectionState("reconnecting");
      } else {
        setConnectionState("disconnected");
      }
    };

    es.addEventListener("entity:changed", (e) => {
      try {
        const data: ServerEvent = JSON.parse(e.data);
        const keys = ENTITY_QUERY_KEYS[data.entityType] ?? [];
        for (const key of [...keys, ...ALWAYS_INVALIDATE]) {
          queryClient.invalidateQueries({ queryKey: key });
        }

        if (data.stateChanges?.length) {
          for (const sc of data.stateChanges) {
            toast.info(`${sc.entityId}: ${sc.oldState} → ${sc.newState} — ${sc.reason}`);
          }
        }
      } catch {
        // ignore malformed events
      }
    });

    es.addEventListener("activity:logged", () => {
      queryClient.invalidateQueries({ queryKey: ["activity-logs"] });
    });

    return () => {
      es.close();
      eventSourceRef.current = null;
      setConnectionState("disconnected");
    };
  }, [projectId, queryClient]);

  return { connectionState };
}
