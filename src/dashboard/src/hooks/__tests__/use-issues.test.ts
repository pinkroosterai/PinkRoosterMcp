import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement, type ReactNode } from "react";
import { useIssues, useIssue, useIssueSummary, useIssueAuditLog, useDeleteIssue } from "../use-issues";

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

describe("useIssues", () => {
  it("fetches issues for a project", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useIssues(1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data![0].name).toBe("Test Bug");
  });

  it("does not fetch when projectId is undefined", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useIssues(undefined), { wrapper });

    expect(result.current.fetchStatus).toBe("idle");
  });

  it("passes state filter as query param", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useIssues(1, "terminal"), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data![0].state).toBe("Completed");
  });
});

describe("useIssue", () => {
  it("fetches a single issue", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useIssue(1, 3), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.issueNumber).toBe(3);
  });
});

describe("useIssueSummary", () => {
  it("fetches issue summary", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useIssueSummary(1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.activeCount).toBe(2);
  });

  it("does not fetch when projectId is undefined", () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useIssueSummary(undefined), { wrapper });

    expect(result.current.fetchStatus).toBe("idle");
  });
});

describe("useIssueAuditLog", () => {
  it("fetches audit log entries", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useIssueAuditLog(1, 1), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(2);
    expect(result.current.data![0].fieldName).toBe("State");
  });
});

describe("useDeleteIssue", () => {
  it("calls delete and succeeds", async () => {
    const { wrapper } = createWrapper();

    const { result } = renderHook(() => useDeleteIssue(), { wrapper });

    result.current.mutate({ projectId: 1, issueNumber: 1 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });
});
