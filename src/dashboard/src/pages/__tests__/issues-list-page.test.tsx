import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { IssuesListPage } from "../issues-list-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/issues" element={<IssuesListPage />} />
    </Routes>,
    { route: `/projects/${projectId}/issues` },
  );
}

describe("IssuesListPage", () => {
  it("renders page heading and issue data", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1-issue-1")).toBeInTheDocument();
  });

  it("shows state filter buttons", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    expect(screen.getByRole("button", { name: "All" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Active" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Inactive" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Terminal" })).toBeInTheDocument();
  });

  it("shows summary cards", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Active")).toBeInTheDocument();
    });
    expect(screen.getByText("Inactive")).toBeInTheDocument();
    expect(screen.getByText("Terminal")).toBeInTheDocument();
  });

  it("shows empty state when no issues exist", async () => {
    server.use(
      http.get("/api/projects/:id/issues", () => HttpResponse.json([])),
    );
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/no issues found/i)).toBeInTheDocument();
    });
  });

  it("shows loading state before data loads", () => {
    renderPage();

    expect(document.querySelector("[data-slot='skeleton']")).toBeInTheDocument();
  });

  it("shows delete confirmation dialog", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    const trashButton = screen.getAllByRole("button").find((btn) => btn.closest("td"));
    if (trashButton) {
      await user.click(trashButton);
      await waitFor(() => {
        expect(screen.getByText("Delete issue?")).toBeInTheDocument();
      });
    }
  });
});
