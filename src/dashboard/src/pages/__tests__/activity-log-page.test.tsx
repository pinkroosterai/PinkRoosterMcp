import { screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { ActivityLogPage } from "../activity-log-page";

describe("ActivityLogPage", () => {
  it("renders page title", () => {
    renderWithProviders(<ActivityLogPage />);
    expect(screen.getByText("Activity Log")).toBeInTheDocument();
  });

  it("renders activity log entries after loading", async () => {
    renderWithProviders(<ActivityLogPage />);

    await waitFor(() => {
      expect(screen.getByText("/api/projects")).toBeInTheDocument();
    });
    expect(screen.getByText("GET")).toBeInTheDocument();
    expect(screen.getByText("POST")).toBeInTheDocument();
  });

  it("shows pagination info", async () => {
    renderWithProviders(<ActivityLogPage />);

    await waitFor(() => {
      expect(screen.getByText(/Showing 2 of 3 entries/)).toBeInTheDocument();
    });
    expect(screen.getByText(/page 1 of 2/i)).toBeInTheDocument();
  });

  it("has Next button enabled on first page", async () => {
    renderWithProviders(<ActivityLogPage />);

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "Next" })).toBeEnabled();
    });
    expect(screen.getByRole("button", { name: "Previous" })).toBeDisabled();
  });

  it("shows empty state when no logs", async () => {
    server.use(
      http.get("/api/activity-logs", () =>
        HttpResponse.json({
          items: [],
          page: 1,
          pageSize: 25,
          totalCount: 0,
          totalPages: 0,
          hasNextPage: false,
          hasPreviousPage: false,
        }),
      ),
    );

    renderWithProviders(<ActivityLogPage />);

    await waitFor(() => {
      expect(screen.getByText("No activity logged yet.")).toBeInTheDocument();
    });
  });

  it("renders caller identity", async () => {
    renderWithProviders(<ActivityLogPage />);

    await waitFor(() => {
      // Both log entries have "mcp-agent" as caller
      expect(screen.getAllByText("mcp-agent").length).toBeGreaterThanOrEqual(1);
    });
  });
});
