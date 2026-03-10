import { http, HttpResponse } from "msw";
import { createFeatureRequest } from "../data/feature-requests";

export const featureRequestHandlers = [
  http.get("/api/projects/:projectId/feature-requests", () => {
    return HttpResponse.json([createFeatureRequest()]);
  }),

  http.get("/api/projects/:projectId/feature-requests/:frNumber", ({ params }) => {
    return HttpResponse.json(
      createFeatureRequest({ featureRequestNumber: Number(params.frNumber) }),
    );
  }),

  http.delete("/api/projects/:projectId/feature-requests/:frNumber", () => {
    return HttpResponse.json(null, { status: 200 });
  }),
];
