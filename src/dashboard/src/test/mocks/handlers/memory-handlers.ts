import { http, HttpResponse } from "msw";
import { createMemoryListItem, createMemory } from "../data/memories";

export const memoryHandlers = [
  http.get("/api/projects/:projectId/memories", () => {
    return HttpResponse.json([
      createMemoryListItem(),
      createMemoryListItem({
        memoryId: "proj-1-mem-2",
        name: "Testing Patterns",
        tags: ["testing"],
        updatedAt: "2026-03-02T08:00:00Z",
      }),
    ]);
  }),

  http.get("/api/projects/:projectId/memories/:memoryNumber", () => {
    return HttpResponse.json(createMemory());
  }),

  http.post("/api/projects/:projectId/memories", async ({ request }) => {
    const body = (await request.json()) as { name: string; content: string; tags?: string[] };
    return HttpResponse.json(
      createMemory({
        name: body.name,
        content: body.content,
        tags: body.tags ?? [],
        wasMerged: false,
      }),
    );
  }),

  http.delete("/api/projects/:projectId/memories/:memoryNumber", () => {
    return new HttpResponse(null, { status: 204 });
  }),
];
