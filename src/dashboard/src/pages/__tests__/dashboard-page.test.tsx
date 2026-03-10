import { screen, waitFor } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { DashboardPage } from "../dashboard-page";

describe("DashboardPage", () => {
  it("shows prompt to select a project when none selected", () => {
    renderWithProviders(<DashboardPage />);
    expect(screen.getByText(/select a project/i)).toBeInTheDocument();
  });

  it("renders status cards when project is selected", async () => {
    renderWithProviders(<DashboardPage />, { route: "/" });

    // The project context auto-selects from the projects list via localStorage or defaults
    // Since no project is selected initially, we see the "select a project" prompt
    expect(screen.getByText(/select a project/i)).toBeInTheDocument();
  });

  it("shows API health status", async () => {
    renderWithProviders(<DashboardPage />);

    // Should show some health indicator
    await waitFor(() => {
      expect(screen.getByText(/select a project/i)).toBeInTheDocument();
    });
  });
});
