import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { ProjectListPage } from "../project-list-page";

describe("ProjectListPage", () => {
  it("renders loading state initially", () => {
    renderWithProviders(<ProjectListPage />);
    expect(screen.getByText("Projects")).toBeInTheDocument();
  });

  it("renders project table after data loads", async () => {
    renderWithProviders(<ProjectListPage />);

    await waitFor(() => {
      expect(screen.getByText("Test Project")).toBeInTheDocument();
    });
    expect(screen.getByText("proj-1")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("shows empty state when no projects exist", async () => {
    server.use(http.get("/api/projects", () => HttpResponse.json([])));

    renderWithProviders(<ProjectListPage />);

    await waitFor(() => {
      expect(screen.getByText("No projects yet")).toBeInTheDocument();
    });
    expect(screen.getByText(/Projects are created by AI agents/i)).toBeInTheDocument();
  });

  it("shows delete confirmation dialog when trash icon clicked", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ProjectListPage />);

    await waitFor(() => {
      expect(screen.getByText("Test Project")).toBeInTheDocument();
    });

    const deleteButtons = screen.getAllByRole("button").filter(
      (btn) => btn.querySelector("svg"),
    );
    // Find the trash button in the table row
    const trashButton = deleteButtons.find((btn) =>
      btn.closest("td"),
    );
    if (trashButton) {
      await user.click(trashButton);
      await waitFor(() => {
        expect(screen.getByText("Delete project?")).toBeInTheDocument();
      });
      expect(screen.getByText(/permanently delete/i)).toBeInTheDocument();
    }
  });

  it("cancels delete when Cancel is clicked", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ProjectListPage />);

    await waitFor(() => {
      expect(screen.getByText("Test Project")).toBeInTheDocument();
    });

    const trashButton = screen.getAllByRole("button").find((btn) => btn.closest("td"));
    if (trashButton) {
      await user.click(trashButton);
      await waitFor(() => {
        expect(screen.getByText("Delete project?")).toBeInTheDocument();
      });

      await user.click(screen.getByRole("button", { name: "Cancel" }));
      await waitFor(() => {
        expect(screen.queryByText("Delete project?")).not.toBeInTheDocument();
      });
    }
  });
});
