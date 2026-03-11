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

  http.patch("/api/projects/:projectId/feature-requests/:frNumber", async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>;
    return HttpResponse.json(
      createFeatureRequest({
        featureRequestNumber: Number(params.frNumber),
        ...body,
      } as Partial<import("@/types").FeatureRequest>),
    );
  }),

  http.post("/api/projects/:projectId/feature-requests/:frNumber/user-stories/manage", async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>;
    const fr = createFeatureRequest({ featureRequestNumber: Number(params.frNumber) });
    const stories = [...fr.userStories];

    if (body.action === "Add") {
      stories.push({ role: body.role as string, goal: body.goal as string, benefit: body.benefit as string });
    } else if (body.action === "Update" && typeof body.index === "number") {
      stories[body.index] = { role: body.role as string, goal: body.goal as string, benefit: body.benefit as string };
    } else if (body.action === "Remove" && typeof body.index === "number") {
      stories.splice(body.index, 1);
    }

    return HttpResponse.json(createFeatureRequest({ featureRequestNumber: Number(params.frNumber), userStories: stories }));
  }),

  http.delete("/api/projects/:projectId/feature-requests/:frNumber", () => {
    return HttpResponse.json(null, { status: 200 });
  }),
];
