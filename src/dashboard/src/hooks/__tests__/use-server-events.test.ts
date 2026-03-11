import { renderHook, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement, type ReactNode } from "react";
import { useServerEvents } from "../use-server-events";

// Mock EventSource
class MockEventSource {
  static instances: MockEventSource[] = [];
  url: string;
  readyState = 0; // CONNECTING
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  private listeners: Record<string, ((e: { data: string }) => void)[]> = {};

  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSED = 2;

  constructor(url: string) {
    this.url = url;
    MockEventSource.instances.push(this);
  }

  addEventListener(type: string, cb: (e: { data: string }) => void) {
    this.listeners[type] = this.listeners[type] || [];
    this.listeners[type].push(cb);
  }

  removeEventListener() {}

  close() {
    this.readyState = MockEventSource.CLOSED;
  }

  // Test helpers
  simulateOpen() {
    this.readyState = MockEventSource.OPEN;
    this.onopen?.();
  }

  simulateError() {
    this.readyState = MockEventSource.CONNECTING;
    this.onerror?.();
  }

  simulateEvent(type: string, data: object) {
    const handlers = this.listeners[type] || [];
    for (const handler of handlers) {
      handler({ data: JSON.stringify(data) });
    }
  }
}

// Install mock
const OriginalEventSource = globalThis.EventSource;
beforeAll(() => {
  (globalThis as unknown as Record<string, unknown>).EventSource = MockEventSource;
});
afterAll(() => {
  (globalThis as unknown as Record<string, unknown>).EventSource = OriginalEventSource;
});
afterEach(() => {
  MockEventSource.instances = [];
});

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return {
    wrapper: ({ children }: { children: ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children),
    queryClient,
  };
}

describe("useServerEvents", () => {
  it("starts disconnected when no projectId", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useServerEvents(undefined), { wrapper });
    expect(result.current.connectionState).toBe("disconnected");
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it("connects to SSE endpoint when projectId is provided", () => {
    const { wrapper } = createWrapper();
    renderHook(() => useServerEvents(1), { wrapper });
    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toBe("/api/projects/1/events");
  });

  it("reports connected state on open", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useServerEvents(1), { wrapper });

    act(() => {
      MockEventSource.instances[0].simulateOpen();
    });

    expect(result.current.connectionState).toBe("connected");
  });

  it("reports reconnecting state on error", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useServerEvents(1), { wrapper });

    act(() => {
      MockEventSource.instances[0].simulateOpen();
    });
    act(() => {
      MockEventSource.instances[0].simulateError();
    });

    expect(result.current.connectionState).toBe("reconnecting");
  });

  it("invalidates queries on entity:changed event", () => {
    const { wrapper, queryClient } = createWrapper();
    const spy = vi.spyOn(queryClient, "invalidateQueries");

    renderHook(() => useServerEvents(1), { wrapper });

    act(() => {
      MockEventSource.instances[0].simulateOpen();
      MockEventSource.instances[0].simulateEvent("entity:changed", {
        eventType: "entity:changed",
        entityType: "Issue",
        entityId: "proj-1-issue-1",
        action: "updated",
        projectId: 1,
      });
    });

    // Should invalidate issue keys + always-invalidate keys
    const calls = spy.mock.calls.map((c) => c[0]);
    const keys = calls.map((c) => (c as { queryKey: string[] }).queryKey);
    expect(keys).toEqual(
      expect.arrayContaining([
        ["issues"],
        ["issue"],
        ["issue-summary"],
        ["project-status"],
        ["next-actions"],
      ]),
    );
  });

  it("invalidates activity-logs on activity:logged event", () => {
    const { wrapper, queryClient } = createWrapper();
    const spy = vi.spyOn(queryClient, "invalidateQueries");

    renderHook(() => useServerEvents(1), { wrapper });

    act(() => {
      MockEventSource.instances[0].simulateOpen();
      MockEventSource.instances[0].simulateEvent("activity:logged", {
        eventType: "activity:logged",
        entityType: "ActivityLog",
        entityId: "/api/projects/1/issues",
        action: "POST",
        projectId: 1,
      });
    });

    const calls = spy.mock.calls.map((c) => c[0]);
    const keys = calls.map((c) => (c as { queryKey: string[] }).queryKey);
    expect(keys).toEqual(expect.arrayContaining([["activity-logs"]]));
  });

  it("closes EventSource on unmount", () => {
    const { wrapper } = createWrapper();
    const { unmount } = renderHook(() => useServerEvents(1), { wrapper });

    const es = MockEventSource.instances[0];
    unmount();

    expect(es.readyState).toBe(MockEventSource.CLOSED);
  });
});
