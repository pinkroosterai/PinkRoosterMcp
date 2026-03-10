import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { FeatureRequestsListPage } from "../feature-requests-list-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/feature-requests" element={<FeatureRequestsListPage />} />
    </Routes>,
    { route: `/projects/${projectId}/feature-requests` },
  );
}

describe("FeatureRequestsListPage", () => {
  it("renders page heading and FR data", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Dashboard Dark Mode")).toBeInTheDocument();
    });
  });

  it("shows state filter buttons", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Dashboard Dark Mode")).toBeInTheDocument();
    });

    expect(screen.getByRole("button", { name: "All" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Active" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Inactive" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Terminal" })).toBeInTheDocument();
  });

  it("shows empty state when no FRs exist", async () => {
    server.use(
      http.get("/api/projects/:id/feature-requests", () => HttpResponse.json([])),
    );
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/no feature requests found/i)).toBeInTheDocument();
    });
  });

  it("shows delete confirmation dialog", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Dashboard Dark Mode")).toBeInTheDocument();
    });

    const trashButton = screen.getAllByRole("button").find((btn) => btn.closest("td"));
    if (trashButton) {
      await user.click(trashButton);
      await waitFor(() => {
        expect(screen.getByText("Delete feature request?")).toBeInTheDocument();
      });
    }
  });
});
