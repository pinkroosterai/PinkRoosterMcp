import { http, HttpResponse } from "msw";
import { createPaginatedLogs, createActivityLog } from "../data/activity";

export const activityHandlers = [
  http.get("/api/activity-logs", ({ request }) => {
    const url = new URL(request.url);
    const page = Number(url.searchParams.get("page") ?? "1");
    const pageSize = Number(url.searchParams.get("pageSize") ?? "25");

    if (page === 1) {
      return HttpResponse.json(
        createPaginatedLogs(
          [
            createActivityLog(),
            createActivityLog({ id: 2, httpMethod: "POST", path: "/api/projects/1/issues", statusCode: 201 }),
          ],
          { page, pageSize, totalCount: 3, totalPages: 2, hasNextPage: true },
        ),
      );
    }
    return HttpResponse.json(
      createPaginatedLogs(
        [createActivityLog({ id: 3, httpMethod: "DELETE", path: "/api/projects/1/issues/1", statusCode: 204 })],
        { page, pageSize, totalCount: 3, totalPages: 2, hasPreviousPage: true },
      ),
    );
  }),
];
