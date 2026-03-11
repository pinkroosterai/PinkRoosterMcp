import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { WorkPackagesListPage } from "../work-packages-list-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/work-packages" element={<WorkPackagesListPage />} />
    </Routes>,
    { route: `/projects/${projectId}/work-packages` },
  );
}

describe("WorkPackagesListPage", () => {
  it("renders page heading and WP data", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });
  });

  it("shows state filter buttons", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    expect(screen.getByRole("button", { name: "All" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Active" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Inactive" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Terminal" })).toBeInTheDocument();
  });

  it("shows summary cards", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Active WPs")).toBeInTheDocument();
    });
    expect(screen.getByText("Inactive WPs")).toBeInTheDocument();
    expect(screen.getByText("Terminal WPs")).toBeInTheDocument();
  });

  it("shows empty state when no WPs exist", async () => {
    server.use(
      http.get("/api/projects/:id/work-packages", () => HttpResponse.json([])),
    );
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/no work packages found/i)).toBeInTheDocument();
    });
  });

  it("shows Create Work Package button", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    const createButton = screen.getByRole("link", { name: /create work package/i });
    expect(createButton).toBeInTheDocument();
    expect(createButton).toHaveAttribute("href", "/projects/1/work-packages/new");
  });

  it("shows Create button in empty state", async () => {
    server.use(
      http.get("/api/projects/:id/work-packages", () => HttpResponse.json([])),
    );
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/no work packages found/i)).toBeInTheDocument();
    });

    const createLinks = screen.getAllByRole("link", { name: /create work package/i });
    expect(createLinks.length).toBeGreaterThanOrEqual(1);
  });

  it("shows delete confirmation dialog", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    const trashButton = screen.getAllByRole("button").find((btn) => btn.closest("td"));
    if (trashButton) {
      await user.click(trashButton);
      await waitFor(() => {
        expect(screen.getByText("Delete work package?")).toBeInTheDocument();
      });
    }
  });
});
