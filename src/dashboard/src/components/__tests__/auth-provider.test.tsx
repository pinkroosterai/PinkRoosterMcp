import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { server } from "@/test/mocks/server";
import { AuthProvider, useAuth } from "../auth-provider";

function createQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
}

function AuthStatus() {
  const { isProtected, isAuthenticated, isLoading, login, logout } = useAuth();

  if (isLoading) return <p>Loading...</p>;

  return (
    <div>
      <p>Protected: {String(isProtected)}</p>
      <p>Authenticated: {String(isAuthenticated)}</p>
      <button onClick={() => login("admin@test.com", "secret")}>Login</button>
      <button onClick={() => login("admin@test.com", "wrong")}>Bad Login</button>
      <button onClick={() => logout()}>Logout</button>
    </div>
  );
}

function renderAuth() {
  const queryClient = createQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthProvider>
          <AuthStatus />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("AuthProvider", () => {
  it("renders children when auth is not protected", async () => {
    server.use(
      http.get("/api/auth/config", () =>
        HttpResponse.json({ isProtected: false }),
      ),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Protected: false")).toBeInTheDocument();
    });
    expect(screen.getByText("Authenticated: false")).toBeInTheDocument();
  });

  it("shows unauthenticated when protected and no session", async () => {
    server.use(
      http.get("/api/auth/config", () =>
        HttpResponse.json({ isProtected: true }),
      ),
      http.get("/api/auth/me", () =>
        new HttpResponse(null, { status: 401 }),
      ),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Protected: true")).toBeInTheDocument();
    });
    expect(screen.getByText("Authenticated: false")).toBeInTheDocument();
  });

  it("shows authenticated when protected with valid session", async () => {
    server.use(
      http.get("/api/auth/config", () =>
        HttpResponse.json({ isProtected: true }),
      ),
      http.get("/api/auth/me", () =>
        HttpResponse.json({
          id: 1,
          email: "admin@test.com",
          displayName: "Admin",
          globalRole: "SuperUser",
          isActive: true,
        }),
      ),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Authenticated: true")).toBeInTheDocument();
    });
  });

  it("login transitions to authenticated on success", async () => {
    const user = userEvent.setup();

    server.use(
      http.get("/api/auth/config", () =>
        HttpResponse.json({ isProtected: true }),
      ),
      http.get("/api/auth/me", () =>
        new HttpResponse(null, { status: 401 }),
      ),
      http.post("/api/auth/login", async ({ request }) => {
        const body = (await request.json()) as { email: string; password: string };
        if (body.email === "admin@test.com" && body.password === "secret") {
          return HttpResponse.json({
            user: { id: 1, email: "admin@test.com", displayName: "Admin", globalRole: "SuperUser", isActive: true },
            expiresAt: new Date(Date.now() + 86400000).toISOString(),
          });
        }
        return HttpResponse.json({ message: "Invalid credentials" }, { status: 401 });
      }),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Authenticated: false")).toBeInTheDocument();
    });

    await user.click(screen.getByText("Login"));

    await waitFor(() => {
      expect(screen.getByText("Authenticated: true")).toBeInTheDocument();
    });
  });

  it("login returns error on invalid credentials", async () => {
    const user = userEvent.setup();
    let loginError: string | null = null;

    function AuthWithError() {
      const { isLoading, login } = useAuth();
      if (isLoading) return <p>Loading...</p>;
      return (
        <div>
          <button
            onClick={async () => {
              loginError = await login("admin@test.com", "wrong");
            }}
          >
            Bad Login
          </button>
          {loginError && <p>Error: {loginError}</p>}
        </div>
      );
    }

    server.use(
      http.get("/api/auth/config", () =>
        HttpResponse.json({ isProtected: true }),
      ),
      http.get("/api/auth/me", () =>
        new HttpResponse(null, { status: 401 }),
      ),
      http.post("/api/auth/login", () =>
        HttpResponse.json({ message: "Invalid credentials" }, { status: 401 }),
      ),
    );

    const queryClient = createQueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <AuthProvider>
            <AuthWithError />
          </AuthProvider>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText("Bad Login")).toBeInTheDocument();
    });

    await user.click(screen.getByText("Bad Login"));

    await waitFor(() => {
      expect(loginError).toBe("Invalid credentials");
    });
  });

  it("logout clears authenticated state", async () => {
    const user = userEvent.setup();

    server.use(
      http.get("/api/auth/config", () =>
        HttpResponse.json({ isProtected: true }),
      ),
      http.get("/api/auth/me", () =>
        HttpResponse.json({
          id: 1,
          email: "admin@test.com",
          displayName: "Admin",
          globalRole: "SuperUser",
          isActive: true,
        }),
      ),
      http.post("/api/auth/logout", () => HttpResponse.json({ success: true })),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Authenticated: true")).toBeInTheDocument();
    });

    await user.click(screen.getByText("Logout"));

    await waitFor(() => {
      expect(screen.getByText("Authenticated: false")).toBeInTheDocument();
    });
  });

  it("treats network errors as auth error", async () => {
    server.use(
      http.get("/api/auth/config", () => HttpResponse.error()),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Protected: false")).toBeInTheDocument();
    });
    expect(screen.getByText("Authenticated: false")).toBeInTheDocument();
  });
});
