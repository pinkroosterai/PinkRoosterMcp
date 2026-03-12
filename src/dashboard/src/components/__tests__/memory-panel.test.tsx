import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeAll } from "vitest";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { MemoryPanel } from "../memory-panel";

// Radix Tooltip/Popper uses ResizeObserver
beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
});

function renderPanel(projectId = 1) {
  return renderWithProviders(<MemoryPanel projectId={projectId} />);
}

describe("MemoryPanel", () => {
  describe("trigger button", () => {
    it("renders the brain icon button", () => {
      renderPanel();
      expect(screen.getByRole("button")).toBeInTheDocument();
    });
  });

  describe("memory list", () => {
    it("opens sheet and displays memories", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });
      expect(screen.getByText("Testing Patterns")).toBeInTheDocument();
    });

    it("displays tags on memory items", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("architecture")).toBeInTheDocument();
      });
      expect(screen.getByText("decisions")).toBeInTheDocument();
      expect(screen.getByText("testing")).toBeInTheDocument();
    });

    it("shows empty state when no memories", async () => {
      server.use(
        http.get("/api/projects/:projectId/memories", () => {
          return HttpResponse.json([]);
        }),
      );

      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("No memories found.")).toBeInTheDocument();
      });
    });

    it("renders search input", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Search memories...")).toBeInTheDocument();
      });
    });

    it("renders create button", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Search memories...")).toBeInTheDocument();
      });

      // The plus button should be present alongside the search input
      const buttons = screen.getAllByRole("button");
      // At least: close (sheet), plus (create)
      expect(buttons.length).toBeGreaterThanOrEqual(2);
    });

    it("displays formatted dates on memory items", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      // The date "2026-03-01T12:00:00Z" should be rendered as a locale date string
      expect(screen.getByText(new Date("2026-03-01T12:00:00Z").toLocaleDateString())).toBeInTheDocument();
    });
  });

  describe("memory detail", () => {
    it("shows memory content when clicking an item", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      await user.click(screen.getByText("Architecture Decisions"));

      await waitFor(() => {
        expect(
          screen.getByText("We use vertical slice architecture with shared DTOs."),
        ).toBeInTheDocument();
      });
    });

    it("shows back button in detail view", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      await user.click(screen.getByText("Architecture Decisions"));

      await waitFor(() => {
        expect(screen.getByText("← Back")).toBeInTheDocument();
      });
    });

    it("navigates back to list when clicking back", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      await user.click(screen.getByText("Architecture Decisions"));

      await waitFor(() => {
        expect(screen.getByText("← Back")).toBeInTheDocument();
      });

      await user.click(screen.getByText("← Back"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Search memories...")).toBeInTheDocument();
      });
    });

    it("shows memory ID and updated date", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      await user.click(screen.getByText("Architecture Decisions"));

      await waitFor(() => {
        expect(screen.getByText(/proj-1-mem-1/)).toBeInTheDocument();
      });
    });

    it("shows tags in detail view", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      await user.click(screen.getByText("Architecture Decisions"));

      await waitFor(() => {
        expect(screen.getByText("We use vertical slice architecture with shared DTOs.")).toBeInTheDocument();
      });
      expect(screen.getByText("architecture")).toBeInTheDocument();
      expect(screen.getByText("decisions")).toBeInTheDocument();
    });

    it("shows loading state", async () => {
      server.use(
        http.get("/api/projects/:projectId/memories/:memoryNumber", async () => {
          await new Promise((r) => setTimeout(r, 100));
          return HttpResponse.json({
            memoryId: "proj-1-mem-1",
            projectId: "proj-1",
            memoryNumber: 1,
            name: "Architecture Decisions",
            content: "Content here",
            tags: [],
            createdAt: "2026-03-01T10:00:00Z",
            updatedAt: "2026-03-01T12:00:00Z",
            wasMerged: false,
          });
        }),
      );

      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      await user.click(screen.getByText("Architecture Decisions"));

      expect(screen.getByText("Loading...")).toBeInTheDocument();
    });

    it("shows not found state for missing memory", async () => {
      server.use(
        http.get("/api/projects/:projectId/memories/:memoryNumber", () => {
          return HttpResponse.json(null);
        }),
      );

      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      await user.click(screen.getByText("Architecture Decisions"));

      await waitFor(() => {
        expect(screen.getByText("Memory not found.")).toBeInTheDocument();
      });
    });

    it("shows delete button in detail view", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByText("Architecture Decisions")).toBeInTheDocument();
      });

      await user.click(screen.getByText("Architecture Decisions"));

      await waitFor(() => {
        expect(screen.getByText("We use vertical slice architecture with shared DTOs.")).toBeInTheDocument();
      });

      const deleteButton = screen.getAllByRole("button").find((b) =>
        b.classList.contains("text-destructive"),
      );
      expect(deleteButton).toBeDefined();
    });
  });

  describe("create memory", () => {
    it("shows create form when clicking plus button", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Search memories...")).toBeInTheDocument();
      });

      // The plus/create button is the outline variant next to search
      const createButton = screen.getAllByRole("button").find(
        (b) => b.getAttribute("data-slot") !== "sheet-close" && b.closest(".flex.gap-2"),
      );
      expect(createButton).toBeDefined();
      await user.click(createButton!);

      await waitFor(() => {
        expect(screen.getByText("New Memory")).toBeInTheDocument();
      });
      expect(screen.getByPlaceholderText("Memory name")).toBeInTheDocument();
      expect(screen.getByPlaceholderText("Content (markdown supported)...")).toBeInTheDocument();
      expect(screen.getByPlaceholderText("Tags (comma-separated)")).toBeInTheDocument();
    });

    it("submits create form with name, content, and tags", async () => {
      let capturedBody: { name: string; content: string; tags?: string[] } | undefined;
      server.use(
        http.post("/api/projects/:projectId/memories", async ({ request }) => {
          capturedBody = (await request.json()) as typeof capturedBody;
          return HttpResponse.json({
            memoryId: "proj-1-mem-3",
            projectId: "proj-1",
            memoryNumber: 3,
            name: capturedBody!.name,
            content: capturedBody!.content,
            tags: capturedBody!.tags ?? [],
            createdAt: "2026-03-11T10:00:00Z",
            updatedAt: "2026-03-11T10:00:00Z",
            wasMerged: false,
          });
        }),
      );

      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Search memories...")).toBeInTheDocument();
      });

      const createButton = screen.getAllByRole("button").find(
        (b) => b.getAttribute("data-slot") !== "sheet-close" && b.closest(".flex.gap-2"),
      );
      await user.click(createButton!);

      await waitFor(() => {
        expect(screen.getByText("New Memory")).toBeInTheDocument();
      });

      await user.type(screen.getByPlaceholderText("Memory name"), "New Decision");
      await user.type(
        screen.getByPlaceholderText("Content (markdown supported)..."),
        "We decided to use PostgreSQL.",
      );
      await user.type(screen.getByPlaceholderText("Tags (comma-separated)"), "db, postgres");

      await user.click(screen.getByRole("button", { name: "Save Memory" }));

      await waitFor(() => {
        expect(capturedBody).toBeDefined();
      });
      expect(capturedBody!.name).toBe("New Decision");
      expect(capturedBody!.content).toBe("We decided to use PostgreSQL.");
      expect(capturedBody!.tags).toEqual(["db", "postgres"]);
    });

    it("navigates back from create form", async () => {
      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Search memories...")).toBeInTheDocument();
      });

      const createButton = screen.getAllByRole("button").find(
        (b) => b.getAttribute("data-slot") !== "sheet-close" && b.closest(".flex.gap-2"),
      );
      await user.click(createButton!);

      await waitFor(() => {
        expect(screen.getByText("New Memory")).toBeInTheDocument();
      });

      await user.click(screen.getByText("← Back"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Search memories...")).toBeInTheDocument();
      });
    });

    it("does not submit when name is empty", async () => {
      let submitted = false;
      server.use(
        http.post("/api/projects/:projectId/memories", () => {
          submitted = true;
          return HttpResponse.json({});
        }),
      );

      const user = userEvent.setup();
      renderPanel();

      await user.click(screen.getByRole("button"));

      await waitFor(() => {
        expect(screen.getByPlaceholderText("Search memories...")).toBeInTheDocument();
      });

      const createButton = screen.getAllByRole("button").find(
        (b) => b.getAttribute("data-slot") !== "sheet-close" && b.closest(".flex.gap-2"),
      );
      await user.click(createButton!);

      await waitFor(() => {
        expect(screen.getByText("New Memory")).toBeInTheDocument();
      });

      // Only fill content, leave name empty
      await user.type(
        screen.getByPlaceholderText("Content (markdown supported)..."),
        "Some content",
      );
      await user.click(screen.getByRole("button", { name: "Save Memory" }));

      // Form uses HTML required — should not have submitted
      expect(submitted).toBe(false);
    });
  });
});
