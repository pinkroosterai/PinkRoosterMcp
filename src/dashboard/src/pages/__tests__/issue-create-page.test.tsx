import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderWithProviders } from "@/test/render";
import { IssueCreatePage } from "../issue-create-page";
import { Route, Routes } from "react-router";
import { server } from "@/test/mocks/server";
import { createIssue } from "@/test/mocks/data/issues";

function renderPage(projectId = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/issues/new" element={<IssueCreatePage />} />
      <Route path="/projects/:id/issues/:issueNumber" element={<div>Issue Detail</div>} />
    </Routes>,
    { route: `/projects/${projectId}/issues/new` },
  );
}

describe("IssueCreatePage", () => {
  it("renders form with required fields", () => {
    renderPage();

    expect(screen.getByRole("heading", { name: /create issue/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Brief issue title")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Describe the issue in detail...")).toBeInTheDocument();
    expect(screen.getByText("Type")).toBeInTheDocument();
    expect(screen.getByText("Severity")).toBeInTheDocument();
    expect(screen.getByText("Priority")).toBeInTheDocument();
  });

  it("renders optional reproduction details section", () => {
    renderPage();

    expect(screen.getByText("Reproduction Details (Optional)")).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/1\. Go to/)).toBeInTheDocument();
    expect(screen.getByPlaceholderText("What should happen")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("What actually happens")).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/Dashboard, API/)).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Paste stack trace here...")).toBeInTheDocument();
  });

  it("validates required fields on empty submit", async () => {
    const user = userEvent.setup();
    renderPage();

    const submitButton = screen.getByRole("button", { name: "Create Issue" });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText("Name is required")).toBeInTheDocument();
    });
    expect(screen.getByText("Description is required")).toBeInTheDocument();
    expect(screen.getByText("Issue type is required")).toBeInTheDocument();
    expect(screen.getByText("Severity is required")).toBeInTheDocument();
  });

  it("renders select dropdowns for type, severity, and priority", () => {
    renderPage();

    const comboboxes = screen.getAllByRole("combobox");
    // Type, Severity, Priority
    expect(comboboxes).toHaveLength(3);
    expect(screen.getByText("Select type")).toBeInTheDocument();
    expect(screen.getByText("Select severity")).toBeInTheDocument();
  });

  it("submit button shows correct text", () => {
    renderPage();

    const submitButton = screen.getByRole("button", { name: "Create Issue" });
    expect(submitButton).toBeInTheDocument();
    expect(submitButton).toHaveAttribute("type", "submit");
  });

  it("shows cancel button that navigates back", async () => {
    renderPage();

    expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();
  });
});
