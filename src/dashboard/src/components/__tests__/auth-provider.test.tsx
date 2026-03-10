import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/mocks/server";
import { renderWithProviders } from "@/test/render";
import { AuthProvider, useAuth } from "../auth-provider";

function AuthStatus() {
  const { isProtected, isAuthenticated, isLoading, login, logout } = useAuth();

  if (isLoading) return <p>Loading...</p>;

  return (
    <div>
      <p>Protected: {String(isProtected)}</p>
      <p>Authenticated: {String(isAuthenticated)}</p>
      <button onClick={() => login("admin", "secret")}>Login</button>
      <button onClick={() => login("admin", "wrong")}>Bad Login</button>
      <button onClick={() => logout()}>Logout</button>
    </div>
  );
}

function renderAuth() {
  return renderWithProviders(
    <AuthProvider>
      <AuthStatus />
    </AuthProvider>,
  );
}

describe("AuthProvider", () => {
  it("renders children when auth is not protected", async () => {
    server.use(
      http.get("/auth/config", () =>
        HttpResponse.json({ protected: false, authenticated: false }),
      ),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Protected: false")).toBeInTheDocument();
    });
    expect(screen.getByText("Authenticated: true")).toBeInTheDocument();
  });

  it("shows unauthenticated when protected and no token", async () => {
    server.use(
      http.get("/auth/config", () =>
        HttpResponse.json({ protected: true, authenticated: false }),
      ),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Protected: true")).toBeInTheDocument();
    });
    expect(screen.getByText("Authenticated: false")).toBeInTheDocument();
  });

  it("shows authenticated when protected with valid token", async () => {
    server.use(
      http.get("/auth/config", () =>
        HttpResponse.json({ protected: true, authenticated: true }),
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
      http.get("/auth/config", () =>
        HttpResponse.json({ protected: true, authenticated: false }),
      ),
      http.post("/auth/login", async ({ request }) => {
        const body = (await request.json()) as {
          username: string;
          password: string;
        };
        if (body.username === "admin" && body.password === "secret") {
          return HttpResponse.json({ token: "test-token-123" });
        }
        return HttpResponse.json(
          { error: "Invalid credentials" },
          { status: 401 },
        );
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
              loginError = await login("admin", "wrong");
            }}
          >
            Bad Login
          </button>
          {loginError && <p>Error: {loginError}</p>}
        </div>
      );
    }

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
        <AuthWithError />
      </AuthProvider>,
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

    let callCount = 0;
    server.use(
      http.get("/auth/config", () => {
        callCount++;
        // First call: authenticated. After logout the state is set directly.
        return HttpResponse.json({ protected: true, authenticated: true });
      }),
      http.post("/auth/logout", () => HttpResponse.json({ success: true })),
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

  it("treats network errors as unprotected", async () => {
    server.use(
      http.get("/auth/config", () => HttpResponse.error()),
    );

    renderAuth();

    await waitFor(() => {
      expect(screen.getByText("Protected: false")).toBeInTheDocument();
    });
    expect(screen.getByText("Authenticated: false")).toBeInTheDocument();
  });
});
