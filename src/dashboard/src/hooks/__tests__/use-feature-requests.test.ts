import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement, type ReactNode } from "react";
import { useFeatureRequests, useFeatureRequest, useDeleteFeatureRequest } from "../use-feature-requests";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  return {
    wrapper: ({ children }: { children: ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children),
    queryClient,
  };
}

describe("useFeatureRequests", () => {
  it("fetches feature requests for a project", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useFeatureRequests(1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data![0].name).toBe("Dashboard Dark Mode");
  });

  it("does not fetch when projectId is undefined", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useFeatureRequests(undefined), { wrapper });

    expect(result.current.fetchStatus).toBe("idle");
  });
});

describe("useFeatureRequest", () => {
  it("fetches a single feature request", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useFeatureRequest(1, 1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.name).toBe("Dashboard Dark Mode");
    expect(result.current.data?.category).toBe("Feature");
  });
});

describe("useDeleteFeatureRequest", () => {
  it("calls delete and succeeds", async () => {
    const { wrapper } = createWrapper();

    const { result } = renderHook(() => useDeleteFeatureRequest(), { wrapper });
    result.current.mutate({ projectId: 1, frNumber: 1 });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });
});
