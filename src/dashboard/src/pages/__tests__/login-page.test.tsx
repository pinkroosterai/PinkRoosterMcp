import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { AuthProvider } from "@/components/auth-provider";
import { LoginPage } from "../login-page";

function renderLogin() {
  server.use(
    http.get("/auth/config", () =>
      HttpResponse.json({ protected: true, authenticated: false }),
    ),
  );

  return renderWithProviders(
    <AuthProvider>
      <LoginPage />
    </AuthProvider>,
  );
}

describe("LoginPage", () => {
  it("renders sign in form", async () => {
    renderLogin();

    await waitFor(() => {
      expect(screen.getByText("PinkRooster")).toBeInTheDocument();
    });
    expect(
      screen.getByText("Sign in to access the dashboard"),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Username")).toBeInTheDocument();
    expect(screen.getByLabelText("Password")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Sign in" }),
    ).toBeInTheDocument();
  });

  it("shows error on invalid credentials", async () => {
    const user = userEvent.setup();

    server.use(
      http.get("/auth/config", () =>
        HttpResponse.json({ protected: true, authenticated: false }),
      ),
      http.post("/auth/login", () =>
        HttpResponse.json(
          { error: "Invalid credentials" },
          { status: 401 },
        ),
      ),
    );

    renderWithProviders(
      <AuthProvider>
        <LoginPage />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByLabelText("Username")).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText("Username"), "admin");
    await user.type(screen.getByLabelText("Password"), "wrong");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => {
      expect(screen.getByText("Invalid credentials")).toBeInTheDocument();
    });
  });

  it("requires username and password fields", async () => {
    renderLogin();

    await waitFor(() => {
      expect(screen.getByLabelText("Username")).toBeInTheDocument();
    });

    expect(screen.getByLabelText("Username")).toBeRequired();
    expect(screen.getByLabelText("Password")).toBeRequired();
  });
});
