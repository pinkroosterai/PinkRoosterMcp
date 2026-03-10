import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement, type ReactNode } from "react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { useProjects, useDeleteProject, useProjectStatus, useNextActions } from "../use-projects";

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

describe("useProjects", () => {
  it("fetches projects", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useProjects(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data![0].name).toBe("Test Project");
  });

  it("handles API errors", async () => {
    server.use(http.get("/api/projects", () => HttpResponse.json(null, { status: 500 })));
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useProjects(), { wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useDeleteProject", () => {
  it("calls delete and succeeds", async () => {
    const { wrapper } = createWrapper();

    const { result } = renderHook(() => useDeleteProject(), { wrapper });

    result.current.mutate(1);
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });
});

describe("useProjectStatus", () => {
  it("fetches project status when projectId is defined", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useProjectStatus(1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.projectId).toBe("proj-1");
    expect(result.current.data?.issues.total).toBe(5);
  });

  it("does not fetch when projectId is undefined", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useProjectStatus(undefined), { wrapper });

    expect(result.current.fetchStatus).toBe("idle");
  });
});

describe("useNextActions", () => {
  it("fetches next actions", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useNextActions(1, 10), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(2);
  });

  it("does not fetch when projectId is undefined", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useNextActions(undefined), { wrapper });

    expect(result.current.fetchStatus).toBe("idle");
  });
});
