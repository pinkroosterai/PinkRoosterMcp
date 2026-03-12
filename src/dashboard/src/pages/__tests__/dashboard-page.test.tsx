import { screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { DashboardPage } from "../dashboard-page";

function renderDashboard() {
  return renderWithProviders(<DashboardPage />);
}

function selectProject() {
  localStorage.setItem("pinkrooster-selected-project-id", "1");
}

describe("DashboardPage", () => {
  afterEach(() => {
    localStorage.clear();
  });

  it("shows prompt to select a project when none selected", () => {
    renderDashboard();
    expect(screen.getByText(/select a project/i)).toBeInTheDocument();
  });

  it("shows View Projects link when no project selected", () => {
    renderDashboard();
    const link = screen.getByRole("link", { name: /view projects/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute("href", "/projects");
  });

  it("renders project name when project is selected", async () => {
    selectProject();
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText("Test Project")).toBeInTheDocument();
    });
  });

  it("renders all three entity summary cards", async () => {
    selectProject();
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText("Issues")).toBeInTheDocument();
    });
    expect(screen.getByText("Feature Requests")).toBeInTheDocument();
    expect(screen.getByText("Work Packages")).toBeInTheDocument();
  });

  it("renders Issues card with active and completed counts", async () => {
    selectProject();
    renderDashboard();

    await waitFor(() => {
      // Issues mock: active=2, terminal=2
      expect(screen.getByText(/2 active/)).toBeInTheDocument();
    });
    // "2 completed" appears only for Issues card
    expect(screen.getByText(/2 completed/)).toBeInTheDocument();
  });

  it("renders progress bars with percentages", async () => {
    selectProject();
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText("Issues")).toBeInTheDocument();
    });
    // Mock data has percent values (40%, 33%, 37%) rendered in MiniDonut
    const progressBars = screen.getAllByRole("progressbar");
    expect(progressBars.length).toBe(3);
  });

  it("renders next actions list with items", async () => {
    selectProject();
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText("Implement feature X")).toBeInTheDocument();
    });
    expect(screen.getByText("Fix critical bug")).toBeInTheDocument();
  });

  it("renders priority badges on next action items", async () => {
    selectProject();
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText("Implement feature X")).toBeInTheDocument();
    });
    expect(screen.getByText("High")).toBeInTheDocument();
    expect(screen.getByText("Critical")).toBeInTheDocument();
  });

  it("renders type badges on next action items", async () => {
    selectProject();
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText("Implement feature X")).toBeInTheDocument();
    });
    // Type icons: Task → "T", Issue → "I"
    expect(screen.getByText("T")).toBeInTheDocument();
    expect(screen.getByText("I")).toBeInTheDocument();
  });

  it("shows 'No actionable items' when next actions is empty", async () => {
    server.use(
      http.get("/api/projects/:id/next-actions", () => HttpResponse.json([])),
    );
    selectProject();
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText("No actionable items.")).toBeInTheDocument();
    });
  });

  it("renders description card when project has a description", async () => {
    selectProject();
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText("Description")).toBeInTheDocument();
    });
  });
});
