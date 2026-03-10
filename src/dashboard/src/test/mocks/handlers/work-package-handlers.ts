import { http, HttpResponse } from "msw";
import { createWorkPackage, createWorkPackageSummary } from "../data/work-packages";

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
