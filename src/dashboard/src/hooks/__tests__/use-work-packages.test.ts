import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement, type ReactNode } from "react";
import { useWorkPackages, useWorkPackage, useWorkPackageSummary, useDeleteWorkPackage, useDeletePhase, useDeleteTask } from "../use-work-packages";

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

describe("useWorkPackages", () => {
  it("fetches work packages for a project", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useWorkPackages(1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data![0].name).toBe("Test Work Package");
  });

  it("does not fetch when projectId is undefined", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useWorkPackages(undefined), { wrapper });

    expect(result.current.fetchStatus).toBe("idle");
  });
});

describe("useWorkPackage", () => {
  it("fetches a single work package", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useWorkPackage(1, 1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.workPackageNumber).toBe(1);
    expect(result.current.data?.phases).toHaveLength(1);
  });
});

describe("useWorkPackageSummary", () => {
  it("fetches work package summary", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useWorkPackageSummary(1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.activeCount).toBe(3);
  });
});

describe("useDeleteWorkPackage", () => {
  it("calls delete and succeeds", async () => {
    const { wrapper } = createWrapper();

    const { result } = renderHook(() => useDeleteWorkPackage(), { wrapper });
    result.current.mutate({ projectId: 1, wpNumber: 1 });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });
});

describe("useDeletePhase", () => {
  it("calls delete and succeeds", async () => {
    const { wrapper } = createWrapper();

    const { result } = renderHook(() => useDeletePhase(), { wrapper });
    result.current.mutate({ projectId: 1, wpNumber: 1, phaseNumber: 1 });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });
});

describe("useDeleteTask", () => {
  it("calls delete and succeeds", async () => {
    const { wrapper } = createWrapper();

    const { result } = renderHook(() => useDeleteTask(), { wrapper });
    result.current.mutate({ projectId: 1, wpNumber: 1, taskNumber: 1 });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });
});
