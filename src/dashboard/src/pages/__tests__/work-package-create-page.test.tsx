import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { WorkPackageCreatePage } from "../work-package-create-page";
import { Route, Routes } from "react-router";

function renderPage(projectId = 1) {
  return renderWithProviders(
    <Routes>
      <Route path="/projects/:id/work-packages/new" element={<WorkPackageCreatePage />} />
      <Route path="/projects/:id/work-packages/:wpNumber" element={<div>WP Detail</div>} />
    </Routes>,
    { route: `/projects/${projectId}/work-packages/new` },
  );
}

describe("WorkPackageCreatePage", () => {
  it("renders form with required fields", async () => {
    renderPage();

    expect(screen.getByRole("heading", { name: /create work package/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Work package title")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Describe the work package...")).toBeInTheDocument();
  });

  it("validates required fields on empty submit", async () => {
    const user = userEvent.setup();
    renderPage();

    const submitButton = screen.getByRole("button", { name: "Create Work Package" });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText("Name is required")).toBeInTheDocument();
    });
    expect(screen.getByText("Description is required")).toBeInTheDocument();
  });

  it("submits form and navigates to detail page", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.type(screen.getByPlaceholderText("Work package title"), "New WP");
    await user.type(screen.getByPlaceholderText("Describe the work package..."), "WP description");

    const submitButton = screen.getByRole("button", { name: "Create Work Package" });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText("WP Detail")).toBeInTheDocument();
    });
  });

  it("renders optional detail fields", async () => {
    renderPage();

    expect(screen.getByText("Details (Optional)")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Implementation plan or approach...")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("e.g. 5")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Why this complexity?")).toBeInTheDocument();
  });

  it("renders entity linking section", async () => {
    renderPage();

    expect(screen.getByText("Link Entities (Optional)")).toBeInTheDocument();
    expect(screen.getByText("Link to Issue")).toBeInTheDocument();
    expect(screen.getByText("Link to Feature Request")).toBeInTheDocument();
  });
});
