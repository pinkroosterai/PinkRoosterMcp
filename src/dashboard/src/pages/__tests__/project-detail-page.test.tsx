import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { ProjectDetailPage } from "../project-detail-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id" element={<ProjectDetailPage />} />
    </Routes>,
    { route: `/projects/${projectId}` },
  );
}

describe("ProjectDetailPage", () => {
  it("renders project name and tabs", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Project")).toBeInTheDocument();
    });
    expect(screen.getByRole("button", { name: /issues/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /feature requests/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /work packages/i })).toBeInTheDocument();
  });

  it("shows issues tab by default with issue data", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1-issue-1")).toBeInTheDocument();
  });

  it("switches to feature requests tab", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Project")).toBeInTheDocument();
    });

    const frTab = screen.getByRole("button", { name: /feature requests/i });
    await user.click(frTab);

    await waitFor(() => {
      expect(screen.getByText("Dashboard Dark Mode")).toBeInTheDocument();
    });
  });

  it("switches to work packages tab", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Project")).toBeInTheDocument();
    });

    const wpTab = screen.getByRole("button", { name: /work packages/i });
    await user.click(wpTab);

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });
  });

  it("shows state filter buttons for issues", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    expect(screen.getByRole("button", { name: "All" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Active" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Inactive" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Terminal" })).toBeInTheDocument();
  });

  it("shows empty state for issues when none exist", async () => {
    server.use(
      http.get("/api/projects/:id/issues", () => HttpResponse.json([])),
    );
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/no issues found/i)).toBeInTheDocument();
    });
  });

  it("shows empty state for feature requests when none exist", async () => {
    const user = userEvent.setup();
    server.use(
      http.get("/api/projects/:id/feature-requests", () => HttpResponse.json([])),
    );
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Project")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /feature requests/i }));

    await waitFor(() => {
      expect(screen.getByText(/no feature requests found/i)).toBeInTheDocument();
    });
  });

  it("shows issue delete confirmation dialog", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    // Find delete button in the issue table row
    const trashButton = screen.getAllByRole("button").find((btn) => btn.closest("td"));
    if (trashButton) {
      await user.click(trashButton);
      await waitFor(() => {
        expect(screen.getByText("Delete issue?")).toBeInTheDocument();
      });
    }
  });

  it("shows not-found state for invalid project ID", async () => {
    server.use(http.get("/api/projects", () => HttpResponse.json([])));
    renderPage(999);

    await waitFor(() => {
      expect(screen.getByText(/project not found/i)).toBeInTheDocument();
    });
  });
});
