import { screen } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { SkillsHelpPage } from "../skills-help-page";

describe("SkillsHelpPage", () => {
  it("renders page title", () => {
    renderWithProviders(<SkillsHelpPage />);
    expect(screen.getByText("PM Workflow Skills")).toBeInTheDocument();
  });

  it("renders all 11 skill names", () => {
    renderWithProviders(<SkillsHelpPage />);

    // Skills whose usage differs from the title have unique title text
    const uniqueNames = [
      "/pm-next",
      "/pm-done",
      "/pm-implement",
      "/pm-scaffold",
      "/pm-plan",
      "/pm-verify",
      "/pm-cleanup",
      "/pm-explore",
    ];

    for (const name of uniqueNames) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }

    // pm-status and pm-triage titles match their usage, so they appear twice
    expect(screen.getAllByText("/pm-status")).toHaveLength(2);
    expect(screen.getAllByText("/pm-triage")).toHaveLength(2);
  });

  it("renders descriptions for each skill", () => {
    renderWithProviders(<SkillsHelpPage />);

    expect(
      screen.getByText(/Show a project status dashboard/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Pick up the next highest-priority/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Mark tasks, issues, or feature requests as completed/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Execute implementation for a task/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Scaffold a complete work package/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Create an issue or feature request from/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Review and prioritize open issues/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Verify acceptance criteria for a phase/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Identify and remove stale, cancelled/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Analyze the codebase from a product manager/),
    ).toBeInTheDocument();
  });

  it("renders usage syntax for each skill", () => {
    renderWithProviders(<SkillsHelpPage />);

    // Skills with unique usage strings (different from card title)
    expect(screen.getByText("/pm-next [entityType]")).toBeInTheDocument();
    expect(screen.getByText("/pm-done <id>")).toBeInTheDocument();
    expect(
      screen.getByText("/pm-implement <id> [--dry-run]"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("/pm-scaffold <description | issue-id | fr-id>"),
    ).toBeInTheDocument();
    expect(screen.getByText("/pm-plan <description>")).toBeInTheDocument();

    // pm-status and pm-triage usage matches their title, so they appear twice
    expect(screen.getAllByText("/pm-status")).toHaveLength(2);
    expect(screen.getAllByText("/pm-triage")).toHaveLength(2);
  });

  it("shows read-only badges for pm-status and pm-triage", () => {
    renderWithProviders(<SkillsHelpPage />);

    const badges = screen.getAllByText("Read-only");
    expect(badges).toHaveLength(4);
  });

  it("shows auto-state propagation for skills that have it", () => {
    renderWithProviders(<SkillsHelpPage />);

    const propagationHeaders = screen.getAllByText("Auto-State Propagation");
    // pm-next, pm-done, pm-implement, pm-scaffold, pm-cleanup = 5 skills with state changes
    expect(propagationHeaders).toHaveLength(5);
  });
});
