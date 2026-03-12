import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { FeatureRequestCreatePage } from "../feature-request-create-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/feature-requests/new" element={<FeatureRequestCreatePage />} />
      <Route path="/projects/:id/feature-requests/:frNumber" element={<div>FR Detail</div>} />
    </Routes>,
    { route: `/projects/${projectId}/feature-requests/new` },
  );
}

describe("FeatureRequestCreatePage", () => {
  it("renders form with required fields", () => {
    renderPage();

    expect(screen.getByRole("heading", { name: /create feature request/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Feature request title")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Describe the feature...")).toBeInTheDocument();
    expect(screen.getByText("Category")).toBeInTheDocument();
    expect(screen.getByText("Priority")).toBeInTheDocument();
  });

  it("renders optional details section", () => {
    renderPage();

    expect(screen.getByText("Details (Optional)")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Why is this valuable?")).toBeInTheDocument();
    expect(screen.getByText("User Stories")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Who requested this?")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("How will we know this is done?")).toBeInTheDocument();
  });

  it("validates required fields on empty submit", async () => {
    const user = userEvent.setup();
    renderPage();

    const submitButton = screen.getByRole("button", { name: "Create Feature Request" });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText("Name is required")).toBeInTheDocument();
    });
    expect(screen.getByText("Description is required")).toBeInTheDocument();
    expect(screen.getByText("Category is required")).toBeInTheDocument();
  });

  it("renders select dropdowns for category and priority", () => {
    renderPage();

    const comboboxes = screen.getAllByRole("combobox");
    // Category, Priority
    expect(comboboxes).toHaveLength(2);
    expect(screen.getByText("Select category")).toBeInTheDocument();
  });

  it("submit button shows correct text", () => {
    renderPage();

    const submitButton = screen.getByRole("button", { name: "Create Feature Request" });
    expect(submitButton).toBeInTheDocument();
    expect(submitButton).toHaveAttribute("type", "submit");
  });

  it("shows cancel button", () => {
    renderPage();

    expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();
  });
});
