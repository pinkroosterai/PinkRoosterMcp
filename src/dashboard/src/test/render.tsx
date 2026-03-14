import { type ReactElement } from "react";
import { render, type RenderOptions } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import type { Project } from "@/types";
import { ProjectProvider } from "@/hooks/use-project-context";
import { AuthContext } from "@/components/auth-provider";

interface CustomRenderOptions extends Omit<RenderOptions, "wrapper"> {
  route?: string;
  initialProject?: Project;
}

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
        staleTime: 0,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

const testAuthValue = {
  isProtected: true,
  isAuthenticated: true,
  isLoading: false,
  authError: false,
  user: { id: 1, email: "test@example.com", displayName: "Test User", globalRole: "SuperUser", isActive: true },
  login: async () => null,
  logout: async () => {},
  register: async () => null,
};

export function renderWithProviders(
  ui: ReactElement,
  options: CustomRenderOptions = {},
) {
  const { route = "/", initialProject, ...renderOptions } = options;
  const queryClient = createTestQueryClient();

  function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          <AuthContext.Provider value={testAuthValue}>
            <ProjectProvider>
              {children}
            </ProjectProvider>
          </AuthContext.Provider>
        </MemoryRouter>
      </QueryClientProvider>
    );
  }

  const result = render(ui, { wrapper: Wrapper, ...renderOptions });

  return {
    ...result,
    queryClient,
  };
}

export { createTestQueryClient };
