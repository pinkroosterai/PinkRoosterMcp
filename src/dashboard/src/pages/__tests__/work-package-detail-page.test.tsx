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
            linkedIssueIds: ["proj-1-issue-3"],
            phases: [],
          }),
        ),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Linked Issues")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1-issue-3")).toBeInTheDocument();
  });

  it("renders linked feature request card", async () => {
    server.use(
      http.get("/api/projects/:id/work-packages/:n", () =>
        HttpResponse.json(
          createWorkPackage({
            linkedFeatureRequestIds: ["proj-1-fr-2"],
            phases: [],
          }),
        ),
      ),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Linked Feature Requests")).toBeInTheDocument();
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

  it("shows Edit button that toggles to edit mode with Save/Cancel", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    // Edit button visible, Save/Cancel not
    expect(screen.getByRole("button", { name: /edit/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /save/i })).not.toBeInTheDocument();

    // Click Edit
    await user.click(screen.getByRole("button", { name: /edit/i }));

    // Save and Cancel now visible, Edit hidden
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /save/i })).toBeInTheDocument();
    });
    expect(screen.getByRole("button", { name: /cancel/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^edit$/i })).not.toBeInTheDocument();
  });

  it("cancel restores read-only mode", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /edit/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /cancel/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /cancel/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /edit/i })).toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: /save/i })).not.toBeInTheDocument();
  });

  it("save sends PATCH with changed fields and exits edit mode", async () => {
    const user = userEvent.setup();
    let patchBody: Record<string, unknown> | null = null;

    server.use(
      http.patch("/api/projects/:id/work-packages/:n", async ({ request }) => {
        patchBody = await request.json() as Record<string, unknown>;
        return HttpResponse.json(
          createWorkPackage({ name: "Updated Name" }),
        );
      }),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /edit/i }));

    // Find the name input and change it
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /save/i })).toBeInTheDocument();
    });

    const nameInput = screen.getByDisplayValue("Test Work Package");
    await user.clear(nameInput);
    await user.type(nameInput, "Updated Name");

    await user.click(screen.getByRole("button", { name: /save/i }));

    await waitFor(() => {
      expect(patchBody).not.toBeNull();
    });
    expect(patchBody!.name).toBe("Updated Name");
    // Should not include unchanged fields
    expect(patchBody!.priority).toBeUndefined();
  });

  it("renders WP state as a select combobox", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    // The WP state should be rendered as a combobox (Select component)
    const stateCombobox = screen.getAllByRole("combobox").find(
      (el) => el.textContent?.includes("NotStarted"),
    );
    expect(stateCombobox).toBeTruthy();
  });

  it("renders task state as a select combobox after expanding phase", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Implementation")).toBeInTheDocument();
    });

    // Expand phase
    await user.click(screen.getByText("Implementation"));

    await waitFor(() => {
      expect(screen.getByText("Implement feature")).toBeInTheDocument();
    });

    // Task state should also be a combobox
    // After expanding, there should be more comboboxes (WP state + task state(s))
    const comboboxes = screen.getAllByRole("combobox");
    expect(comboboxes.length).toBeGreaterThanOrEqual(2);
  });

  it("save with no changes exits edit mode without PATCH", async () => {
    const user = userEvent.setup();
    let patchCalled = false;

    server.use(
      http.patch("/api/projects/:id/work-packages/:n", () => {
        patchCalled = true;
        return HttpResponse.json(createWorkPackage());
      }),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /edit/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /save/i })).toBeInTheDocument();
    });

    // Save without changing anything
    await user.click(screen.getByRole("button", { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /edit/i })).toBeInTheDocument();
    });
    expect(patchCalled).toBe(false);
  });

  it("renders description in edit mode as textarea", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /edit/i }));

    await waitFor(() => {
      expect(screen.getByDisplayValue("A work package for testing")).toBeInTheDocument();
    });
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

  it("handles API 500 error gracefully", async () => {
    server.use(
      http.get("/api/projects/:id/work-packages/:n", () =>
        HttpResponse.json({ error: "Server error" }, { status: 500 }),
      ),
    );
    renderPage(1, 1);

    await waitFor(() => {
      expect(screen.getByText("Work package not found.")).toBeInTheDocument();
    });
  });

  it("shows loading state before data loads", () => {
    renderPage();

    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("closes delete dialog on Escape key", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Work Package")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /delete/i }));

    await waitFor(() => {
      expect(screen.getByText("Delete work package?")).toBeInTheDocument();
    });

    await user.keyboard("{Escape}");

    await waitFor(() => {
      expect(screen.queryByText("Delete work package?")).not.toBeInTheDocument();
    });
  });
});
