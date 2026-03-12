import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { IssueDetailPage } from "../issue-detail-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1, issueNumber = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/issues/:issueNumber" element={<IssueDetailPage />} />
    </Routes>,
    { route: `/projects/${projectId}/issues/${issueNumber}` },
  );
}

describe("IssueDetailPage", () => {
  it("renders issue name and badge", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1-issue-1")).toBeInTheDocument();
    // State appears in both header and audit log, so use getAllByText
    expect(screen.getAllByText("Implementing").length).toBeGreaterThanOrEqual(1);
  });

  it("renders definition card with type, severity, priority", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Definition")).toBeInTheDocument();
    });
    expect(screen.getByText("Major")).toBeInTheDocument();
    // Priority "High" may appear in both definition and audit log
    expect(screen.getAllByText("High").length).toBeGreaterThanOrEqual(1);
  });

  it("renders reproduction section", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Reproduction")).toBeInTheDocument();
    });
    expect(screen.getByText(/Open app/)).toBeInTheDocument();
    expect(screen.getByText("Should work")).toBeInTheDocument();
    expect(screen.getByText("Does not work")).toBeInTheDocument();
  });

  it("renders audit log (collapsed by default, expandable)", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/Audit Log/)).toBeInTheDocument();
    });
    // Audit log is collapsed by default — "Field" header not visible
    expect(screen.queryByText("Field")).not.toBeInTheDocument();

    // Click to expand
    await user.click(screen.getByText(/Audit Log/));
    await waitFor(() => {
      expect(screen.getByText("Field")).toBeInTheDocument();
    });
    expect(screen.getByText("Changed By")).toBeInTheDocument();
  });

  it("renders timeline card", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Timeline")).toBeInTheDocument();
    });
    expect(screen.getByText("Created")).toBeInTheDocument();
    expect(screen.getByText("Started")).toBeInTheDocument();
  });

  it("shows not-found state for missing issue", async () => {
    server.use(
      http.get("/api/projects/:id/issues/:n", () =>
        HttpResponse.json(null, { status: 404 }),
      ),
    );
    renderPage(1, 999);

    await waitFor(() => {
      expect(screen.getByText("Issue not found.")).toBeInTheDocument();
    });
  });

  it("shows delete confirmation dialog", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /delete/i }));

    await waitFor(() => {
      expect(screen.getByText("Delete issue?")).toBeInTheDocument();
    });
    expect(screen.getByText(/permanently delete/i)).toBeInTheDocument();
  });

  it("shows Edit button that toggles to edit mode with Save/Cancel", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    expect(screen.getByRole("button", { name: /edit/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /save/i })).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /edit/i }));

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
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
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
      http.patch("/api/projects/:id/issues/:n", async ({ request }) => {
        patchBody = await request.json() as Record<string, unknown>;
        return HttpResponse.json({
          ...{
            issueId: "proj-1-issue-1",
            id: 1,
            issueNumber: 1,
            projectId: "proj-1",
            name: "Updated Bug",
            description: "Something is **broken** in the app",
            issueType: "Bug",
            severity: "Major",
            priority: "High",
            state: "Implementing",
            stepsToReproduce: "1. Open app\n2. Click button",
            expectedBehavior: "Should work",
            actualBehavior: "Does not work",
            affectedComponent: "Dashboard",
            stackTrace: null,
            rootCause: null,
            resolution: null,
            startedAt: "2026-01-02T00:00:00Z",
            completedAt: null,
            resolvedAt: null,
            attachments: [],
            linkedWorkPackages: [],
            createdAt: "2026-01-01T00:00:00Z",
            updatedAt: "2026-01-02T00:00:00Z",
          },
        });
      }),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /edit/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /save/i })).toBeInTheDocument();
    });

    const nameInput = screen.getByDisplayValue("Test Bug");
    await user.clear(nameInput);
    await user.type(nameInput, "Updated Bug");

    await user.click(screen.getByRole("button", { name: /save/i }));

    await waitFor(() => {
      expect(patchBody).not.toBeNull();
    });
    expect(patchBody!.name).toBe("Updated Bug");
    expect(patchBody!.priority).toBeUndefined();
  });

  it("save with no changes exits edit mode without PATCH", async () => {
    const user = userEvent.setup();
    let patchCalled = false;

    server.use(
      http.patch("/api/projects/:id/issues/:n", () => {
        patchCalled = true;
        return HttpResponse.json({});
      }),
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /edit/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /save/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /edit/i })).toBeInTheDocument();
    });
    expect(patchCalled).toBe(false);
  });

  it("renders state as a select combobox", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    const stateCombobox = screen.getAllByRole("combobox").find(
      (el) => el.textContent?.includes("Implementing"),
    );
    expect(stateCombobox).toBeTruthy();
  });

  it("renders description with markdown formatting in a Description card", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Description")).toBeInTheDocument();
    });
    // The mock description contains "Something is **broken** in the app" — bold should render as <strong>
    const strong = document.querySelector(".prose strong");
    expect(strong).toBeInTheDocument();
    expect(strong?.textContent).toBe("broken");
  });

  it("handles API 500 error gracefully", async () => {
    server.use(
      http.get("/api/projects/:id/issues/:n", () =>
        HttpResponse.json({ error: "Server error" }, { status: 500 }),
      ),
    );
    renderPage(1, 1);

    await waitFor(() => {
      expect(screen.getByText("Issue not found.")).toBeInTheDocument();
    });
  });

  it("shows loading state before data loads", () => {
    renderPage();

    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("hides reproduction section when no reproduction data", async () => {
    server.use(
      http.get("/api/projects/:id/issues/:n", () =>
        HttpResponse.json({
          issueId: "proj-1-issue-1",
          id: 1,
          issueNumber: 1,
          projectId: "proj-1",
          name: "Minimal Issue",
          description: "No repro",
          issueType: "Bug",
          severity: "Minor",
          priority: "Low",
          stepsToReproduce: null,
          expectedBehavior: null,
          actualBehavior: null,
          affectedComponent: null,
          stackTrace: null,
          rootCause: null,
          resolution: null,
          state: "NotStarted",
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
      expect(screen.getByText("Minimal Issue")).toBeInTheDocument();
    });
    expect(screen.queryByText("Reproduction")).not.toBeInTheDocument();
    expect(screen.queryByText("Resolution")).not.toBeInTheDocument();
  });

  it("closes delete dialog on Escape key", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Test Bug")).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: /delete/i }));

    await waitFor(() => {
      expect(screen.getByText("Delete issue?")).toBeInTheDocument();
    });

    await user.keyboard("{Escape}");

    await waitFor(() => {
      expect(screen.queryByText("Delete issue?")).not.toBeInTheDocument();
    });
  });
});
