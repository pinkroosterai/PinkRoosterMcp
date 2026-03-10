import { http, HttpResponse } from "msw";
import { createProject, createProjectStatus, createNextActionItem } from "../data/projects";

export const projectHandlers = [
  http.get("/api/projects", () => {
    return HttpResponse.json([createProject()]);
  }),

  http.delete("/api/projects/:id", () => {
    return HttpResponse.json(null, { status: 200 });
  }),

  http.get("/api/projects/:id/status", () => {
    return HttpResponse.json(createProjectStatus());
  }),

  http.get("/api/projects/:id/next-actions", () => {
    return HttpResponse.json([
      createNextActionItem(),
      createNextActionItem({
        type: "Issue",
        id: "proj-1-issue-1",
        name: "Fix critical bug",
        priority: "Critical",
        state: "Implementing",
        parentId: "proj-1",
      }),
    ]);
  }),
];
