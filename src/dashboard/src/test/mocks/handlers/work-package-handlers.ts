import { http, HttpResponse } from "msw";
import { createWorkPackage, createWorkPackageSummary, createTask } from "../data/work-packages";

export const workPackageHandlers = [
  http.get("/api/projects/:projectId/work-packages", () => {
    return HttpResponse.json([createWorkPackage()]);
  }),

  http.get("/api/projects/:projectId/work-packages/summary", () => {
    return HttpResponse.json(createWorkPackageSummary());
  }),

  http.get("/api/projects/:projectId/work-packages/:wpNumber", ({ params }) => {
    return HttpResponse.json(
      createWorkPackage({ workPackageNumber: Number(params.wpNumber) }),
    );
  }),

  http.post("/api/projects/:projectId/work-packages", async ({ request }) => {
    const body = await request.json() as Record<string, unknown>;
    return HttpResponse.json(
      createWorkPackage({
        workPackageNumber: 99,
        name: body.name as string,
        description: body.description as string,
        ...(body.type ? { type: body.type } : {}),
        ...(body.priority ? { priority: body.priority } : {}),
      } as Partial<import("@/types").WorkPackage>),
      { status: 201 },
    );
  }),

  http.patch("/api/projects/:projectId/work-packages/:wpNumber", async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>;
    return HttpResponse.json(
      createWorkPackage({
        workPackageNumber: Number(params.wpNumber),
        ...body,
        stateChanges: body.state === "Completed"
          ? [{ entityType: "WorkPackage", entityId: `proj-${params.projectId}-wp-${params.wpNumber}`, oldState: "Implementing", newState: "Completed", reason: "State changed" }]
          : undefined,
      } as Partial<import("@/types").WorkPackage>),
    );
  }),

  http.patch("/api/projects/:projectId/work-packages/:wpNumber/tasks/:taskNumber", async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>;
    return HttpResponse.json(
      createTask({
        taskNumber: Number(params.taskNumber),
        ...body,
        stateChanges: body.state === "Completed"
          ? [{ entityType: "Task", entityId: `proj-${params.projectId}-wp-${params.wpNumber}-task-${params.taskNumber}`, oldState: "Implementing", newState: "Completed", reason: "State changed" }]
          : undefined,
      } as Partial<import("@/types").WpTask>),
    );
  }),

  http.delete("/api/projects/:projectId/work-packages/:wpNumber", () => {
    return HttpResponse.json(null, { status: 200 });
  }),

  http.delete("/api/projects/:projectId/work-packages/:wpNumber/phases/:phaseNumber", () => {
    return HttpResponse.json(null, { status: 200 });
  }),

  http.delete("/api/projects/:projectId/work-packages/:wpNumber/tasks/:taskNumber", () => {
    return HttpResponse.json(null, { status: 200 });
  }),
];
