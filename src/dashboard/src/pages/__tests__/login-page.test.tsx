import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { server } from "@/test/mocks/server";
import { AuthProvider } from "@/components/auth-provider";
import { LoginPage } from "../login-page";

function createQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
}

function renderLogin() {
  server.use(
    http.get("/api/auth/config", () =>
      HttpResponse.json({ isProtected: true }),
    ),
    http.get("/api/auth/me", () =>
      new HttpResponse(null, { status: 401 }),
    ),
  );

  const queryClient = createQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthProvider>
          <LoginPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("LoginPage", () => {
  it("renders sign in form", async () => {
    renderLogin();

    await waitFor(() => {
      expect(screen.getByText("Sign in to access the dashboard")).toBeInTheDocument();
    });
    expect(screen.getByText("PinkRoosterMCP")).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toBeInTheDocument();
    expect(screen.getByLabelText("Password")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Sign in" }),
    ).toBeInTheDocument();
  });

  it("shows error on invalid credentials", async () => {
    const user = userEvent.setup();

    server.use(
      http.get("/api/auth/config", () =>
        HttpResponse.json({ isProtected: true }),
      ),
      http.get("/api/auth/me", () =>
        new HttpResponse(null, { status: 401 }),
      ),
      http.post("/api/auth/login", () =>
        HttpResponse.json(
          { message: "Invalid credentials" },
          { status: 401 },
        ),
      ),
    );

    const queryClient = createQueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <AuthProvider>
            <LoginPage />
          </AuthProvider>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText("Sign in to access the dashboard")).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText("Email"), "admin@test.com");
    await user.type(screen.getByLabelText("Password"), "wrong");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => {
      expect(screen.getByText("Invalid credentials")).toBeInTheDocument();
    });
  });

  it("requires email and password fields", async () => {
    renderLogin();

    await waitFor(() => {
      expect(screen.getByText("Sign in to access the dashboard")).toBeInTheDocument();
    });

    expect(screen.getByLabelText("Email")).toBeRequired();
    expect(screen.getByLabelText("Password")).toBeRequired();
  });
});
