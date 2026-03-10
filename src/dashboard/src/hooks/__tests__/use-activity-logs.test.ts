import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement, type ReactNode } from "react";
import { useActivityLogs } from "../use-activity-logs";

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

describe("useActivityLogs", () => {
  it("fetches paginated activity logs", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useActivityLogs(1, 25), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.items).toHaveLength(2);
    expect(result.current.data?.hasNextPage).toBe(true);
  });

  it("fetches second page", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useActivityLogs(2, 25), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.items).toHaveLength(1);
    expect(result.current.data?.hasPreviousPage).toBe(true);
  });
});
