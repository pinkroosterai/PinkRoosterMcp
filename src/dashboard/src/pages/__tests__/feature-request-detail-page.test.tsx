import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { FeatureRequestDetailPage } from "../feature-request-detail-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1, featureNumber = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/feature-requests/:featureNumber" element={<FeatureRequestDetailPage />} />
    </Routes>,
    { route: `/projects/${projectId}/feature-requests/${featureNumber}` },
  );
}

describe("FeatureRequestDetailPage", () => {
  it("renders feature request name and status", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Dashboard Dark Mode")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1-fr-1")).toBeInTheDocument();
    expect(screen.getByText("Approved")).toBeInTheDocument();
  });

  it("renders definition card with category and priority", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Definition")).toBeInTheDocument();
    });
    expect(screen.getByText("Feature")).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();
  });

  it("renders requester field", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("product-team")).toBeInTheDocument();
    });
  });

  it("renders user story and business value", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("User Story & Business Value")).toBeInTheDocument();
    });
    expect(screen.getByText("As a user I want dark mode")).toBeInTheDocument();
    expect(screen.getByText("Improves UX for night users")).toBeInTheDocument();
  });

  it("renders acceptance summary", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Acceptance Summary")).toBeInTheDocument();
    });
    expect(screen.getByText("Theme toggle works in header")).toBeInTheDocument();
  });

  it("renders timeline card", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Timeline")).toBeInTheDocument();
    });
    expect(screen.getByText("Created")).toBeInTheDocument();
  });

  it("shows not-found state for missing FR", async () => {
    server.use(
      http.get("/api/projects/:id/feature-requests/:n", () =>
        HttpResponse.json(null, { status: 404 }),
      ),
    );
    renderPage(1, 999);

    await waitFor(() => {
      expect(screen.getByText("Feature request not found.")).toBeInTheDocument();
    });
  });

  it("shows delete confirmation dialog", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Dashboard Dark Mode")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /delete/i }));

    await waitFor(() => {
      expect(screen.getByText("Delete feature request?")).toBeInTheDocument();
    });
  });

  it("renders linked work packages when present", async () => {
    server.use(
      http.get("/api/projects/:id/feature-requests/:n", () =>
        HttpResponse.json({
          featureRequestId: "proj-1-fr-1",
          id: 1,
          featureRequestNumber: 1,
          projectId: "proj-1",
          name: "FR with WPs",
          description: "Has linked work packages",
          category: "Feature",
          priority: "High",
          status: "InProgress",
          businessValue: null,
          userStory: null,
          requester: null,
          acceptanceSummary: null,
          startedAt: "2026-01-02T00:00:00Z",
          completedAt: null,
          resolvedAt: null,
          attachments: [],
          linkedWorkPackages: [
            { workPackageId: "proj-1-wp-1", name: "Implement Feature", state: "Implementing", type: "Feature", priority: "High" },
          ],
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-02T00:00:00Z",
        }),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Related Work Packages")).toBeInTheDocument();
    });
    expect(screen.getByText("Implement Feature")).toBeInTheDocument();
    expect(screen.getByText("proj-1-wp-1")).toBeInTheDocument();
  });

  it("hides optional sections when data is null", async () => {
    server.use(
      http.get("/api/projects/:id/feature-requests/:n", () =>
        HttpResponse.json({
          featureRequestId: "proj-1-fr-1",
          id: 1,
          featureRequestNumber: 1,
          projectId: "proj-1",
          name: "Minimal FR",
          description: "No optional fields",
          category: "Enhancement",
          priority: "Low",
          status: "Proposed",
          businessValue: null,
          userStory: null,
          requester: null,
          acceptanceSummary: null,
          startedAt: null,
          completedAt: null,
          resolvedAt: null,
          attachments: [],
          linkedWorkPackages: [],
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
        }),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Minimal FR")).toBeInTheDocument();
    });
    expect(screen.queryByText("User Story & Business Value")).not.toBeInTheDocument();
    expect(screen.queryByText("Acceptance Summary")).not.toBeInTheDocument();
    expect(screen.queryByText("Related Work Packages")).not.toBeInTheDocument();
  });
});
