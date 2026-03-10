import { screen, waitFor } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { ProjectDetailPage } from "../project-detail-page";
import { IssuesListPage } from "../issues-list-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id" element={<ProjectDetailPage />} />
      <Route path="/projects/:id/issues" element={<IssuesListPage />} />
    </Routes>,
    { route: `/projects/${projectId}` },
  );
}

describe("ProjectDetailPage", () => {
  it("redirects to issues list page", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Issues")).toBeInTheDocument();
    });
    // Should show issue data from the issues list page
    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });
  });
});
