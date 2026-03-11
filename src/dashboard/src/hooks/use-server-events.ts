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

/** Debounce window (ms) — batches rapid SSE events into a single invalidation pass */
const DEBOUNCE_MS = 150;

export function useServerEvents(projectId: number | undefined) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<ConnectionState>("disconnected");
  const eventSourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    if (!projectId) {
      setConnectionState("disconnected");
      return;
    }

    // Debounce: collect query keys, flush once after DEBOUNCE_MS of quiet
    const pendingKeys = new Set<string>();
    let debounceTimer: ReturnType<typeof setTimeout> | null = null;

    function scheduleFlush() {
      if (debounceTimer) clearTimeout(debounceTimer);
      debounceTimer = setTimeout(() => {
        for (const serialized of pendingKeys) {
          queryClient.invalidateQueries({ queryKey: JSON.parse(serialized) });
        }
        pendingKeys.clear();
        debounceTimer = null;
      }, DEBOUNCE_MS);
    }

    function enqueueKeys(keys: string[][]) {
      for (const key of keys) {
        pendingKeys.add(JSON.stringify(key));
      }
      scheduleFlush();
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
        const entityKeys = ENTITY_QUERY_KEYS[data.entityType] ?? [];
        const scopedKeys = [
          ...entityKeys.map((k) => [...k, data.projectId]),
          ...ALWAYS_INVALIDATE.map((k) => [...k, data.projectId]),
        ];
        enqueueKeys(scopedKeys);

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
      enqueueKeys([["activity-logs"]]);
    });

    return () => {
      es.close();
      eventSourceRef.current = null;
      if (debounceTimer) clearTimeout(debounceTimer);
      setConnectionState("disconnected");
    };
  }, [projectId, queryClient]);

  return { connectionState };
}
