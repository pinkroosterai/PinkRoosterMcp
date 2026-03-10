import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { WorkPackageDetailPage } from "../work-package-detail-page";
import { Route, Routes } from "react-router";
import { createWorkPackage, createPhase, createTask } from "@/test/mocks/data/work-packages";

function renderPage(projectId = 1, wpNumber = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/work-packages/:wpNumber" element={<WorkPackageDetailPage />} />
    </Routes>,
    { route: `/projects/${projectId}/work-packages/${wpNumber}` },
  );
}

describe("WorkPackageDetailPage", () => {
  it("renders work package name and badge", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1-wp-1")).toBeInTheDocument();
  });

  it("renders WP type and priority", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Feature")).toBeInTheDocument();
    });
    expect(screen.getByText("High")).toBeInTheDocument();
  });

  it("renders phases section with phase name", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Phases & Tasks")).toBeInTheDocument();
    });
    expect(screen.getByText("Implementation")).toBeInTheDocument();
  });

  it("expands phase to show tasks", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Implementation")).toBeInTheDocument();
    });

    // Click on the phase card header to expand
    await user.click(screen.getByText("Implementation"));

    await waitFor(() => {
      expect(screen.getByText("Implement feature")).toBeInTheDocument();
    });
  });

  it("shows blocked state with previous active state", async () => {
    server.use(
      http.get("/api/projects/:id/work-packages/:n", () =>
        HttpResponse.json(
          createWorkPackage({
            state: "Blocked",
            previousActiveState: "Implementing",
            phases: [],
          }),
        ),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Blocked")).toBeInTheDocument();
    });
    expect(screen.getByText("(was: Implementing)")).toBeInTheDocument();
  });

  it("renders linked issue as clickable badge", async () => {
    server.use(
      http.get("/api/projects/:id/work-packages/:n", () =>
        HttpResponse.json(
          createWorkPackage({
            linkedIssueId: "proj-1-issue-3",
            phases: [],
          }),
        ),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Linked Issue")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1-issue-3")).toBeInTheDocument();
  });

  it("renders linked feature request card", async () => {
    server.use(
      http.get("/api/projects/:id/work-packages/:n", () =>
        HttpResponse.json(
          createWorkPackage({
            linkedFeatureRequestId: "proj-1-fr-2",
            phases: [],
          }),
        ),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Linked Feature Request")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1-fr-2")).toBeInTheDocument();
  });

  it("shows WP delete confirmation dialog", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /delete/i }));

    await waitFor(() => {
      expect(screen.getByText("Delete work package?")).toBeInTheDocument();
    });
  });

  it("shows dependencies section when present", async () => {
    server.use(
      http.get("/api/projects/:id/work-packages/:n", () =>
        HttpResponse.json(
          createWorkPackage({
            blockedBy: [{ workPackageId: "proj-1-wp-2", name: "Blocker WP", state: "Implementing", reason: null }],
            phases: [],
          }),
        ),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Dependencies")).toBeInTheDocument();
    });
    expect(screen.getByText("Blocker WP")).toBeInTheDocument();
  });

  it("renders task with blocked state indicator", async () => {
    const user = userEvent.setup();

    server.use(
      http.get("/api/projects/:id/work-packages/:n", () =>
        HttpResponse.json(
          createWorkPackage({
            phases: [
              createPhase({
                tasks: [
                  createTask({
                    state: "Blocked",
                    previousActiveState: "Designing",
                  }),
                ],
              }),
            ],
          }),
        ),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Implementation")).toBeInTheDocument();
    });

    await user.click(screen.getByText("Implementation"));

    await waitFor(() => {
      expect(screen.getByText("Implement feature")).toBeInTheDocument();
    });
    expect(screen.getByText("(was: Designing)")).toBeInTheDocument();
  });
});
