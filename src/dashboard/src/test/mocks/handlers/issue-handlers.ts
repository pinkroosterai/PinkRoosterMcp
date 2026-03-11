import { http, HttpResponse } from "msw";
import { createIssue, createIssueSummary, createIssueAuditLog } from "../data/issues";

export const issueHandlers = [
  http.get("/api/projects/:projectId/issues", ({ request }) => {
    const url = new URL(request.url);
    const state = url.searchParams.get("state");
    const issue = createIssue();
    if (state === "terminal") {
      return HttpResponse.json([createIssue({ state: "Completed", name: "Completed Bug" })]);
    }
    return HttpResponse.json([issue]);
  }),

  http.get("/api/projects/:projectId/issues/summary", () => {
    return HttpResponse.json(createIssueSummary());
  }),

  http.get("/api/projects/:projectId/issues/:issueNumber", ({ params }) => {
    return HttpResponse.json(
      createIssue({ issueNumber: Number(params.issueNumber) }),
    );
  }),

  http.get("/api/projects/:projectId/issues/:issueNumber/audit", () => {
    return HttpResponse.json([
      createIssueAuditLog(),
      createIssueAuditLog({ fieldName: "Priority", oldValue: "Low", newValue: "High" }),
    ]);
  }),

  http.patch("/api/projects/:projectId/issues/:issueNumber", async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>;
    return HttpResponse.json(
      createIssue({
        issueNumber: Number(params.issueNumber),
        ...body,
      } as Partial<import("@/types").Issue>),
    );
  }),

  http.delete("/api/projects/:projectId/issues/:issueNumber", () => {
    return HttpResponse.json(null, { status: 200 });
  }),
];
