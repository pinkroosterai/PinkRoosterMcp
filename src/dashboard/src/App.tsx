import { BrowserRouter, Routes, Route } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ProjectProvider } from "@/hooks/use-project-context";
import { ThemeProvider } from "@/components/theme-provider";
import { AuthProvider, useAuth } from "@/components/auth-provider";
import { LoginPage } from "@/pages/login-page";
import { AppLayout } from "@/components/layout/app-layout";
import { DashboardPage } from "@/pages/dashboard-page";
import { ActivityLogPage } from "@/pages/activity-log-page";
import { ProjectListPage } from "@/pages/project-list-page";
import { ProjectDetailPage } from "@/pages/project-detail-page";
import { IssuesListPage } from "@/pages/issues-list-page";
import { FeatureRequestsListPage } from "@/pages/feature-requests-list-page";
import { WorkPackagesListPage } from "@/pages/work-packages-list-page";
import { IssueCreatePage } from "@/pages/issue-create-page";
import { FeatureRequestCreatePage } from "@/pages/feature-request-create-page";
import { IssueDetailPage } from "@/pages/issue-detail-page";
import { WorkPackageDetailPage } from "@/pages/work-package-detail-page";
import { FeatureRequestDetailPage } from "@/pages/feature-request-detail-page";
import { WorkPackageCreatePage } from "@/pages/work-package-create-page";
import { SkillsHelpPage } from "@/pages/skills-help-page";
import { UsersListPage } from "@/pages/users-list-page";
import { UserCreatePage } from "@/pages/user-create-page";
import { UserDetailPage } from "@/pages/user-detail-page";
import { ProfilePage } from "@/pages/profile-page";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
});

function AuthGate({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading, authError } = useAuth();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <p className="text-muted-foreground">Loading...</p>
      </div>
    );
  }

  if (authError) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="text-center space-y-4">
          <p className="text-destructive font-medium">Unable to connect to the API</p>
          <p className="text-sm text-muted-foreground">Check that the server is running and try again.</p>
          <button
            onClick={() => window.location.reload()}
            className="text-sm text-primary underline-offset-4 hover:underline"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <LoginPage />;
  }

  return <>{children}</>;
}

function App() {
  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <TooltipProvider>
            <AuthGate>
              <BrowserRouter>
                <ProjectProvider>
                  <Routes>
                    <Route element={<AppLayout />}>
                      <Route index element={<DashboardPage />} />
                      <Route path="projects" element={<ProjectListPage />} />
                      <Route path="projects/:id" element={<ProjectDetailPage />} />
                      <Route path="projects/:id/issues" element={<IssuesListPage />} />
                      <Route path="projects/:id/issues/new" element={<IssueCreatePage />} />
                      <Route path="projects/:id/issues/:issueNumber" element={<IssueDetailPage />} />
                      <Route path="projects/:id/feature-requests" element={<FeatureRequestsListPage />} />
                      <Route path="projects/:id/feature-requests/new" element={<FeatureRequestCreatePage />} />
                      <Route path="projects/:id/feature-requests/:featureNumber" element={<FeatureRequestDetailPage />} />
                      <Route path="projects/:id/work-packages" element={<WorkPackagesListPage />} />
                      <Route path="projects/:id/work-packages/new" element={<WorkPackageCreatePage />} />
                      <Route path="projects/:id/work-packages/:wpNumber" element={<WorkPackageDetailPage />} />
                      <Route path="users" element={<UsersListPage />} />
                      <Route path="users/new" element={<UserCreatePage />} />
                      <Route path="users/:id" element={<UserDetailPage />} />
                      <Route path="profile" element={<ProfilePage />} />
                      <Route path="activity" element={<ActivityLogPage />} />
                      <Route path="help" element={<SkillsHelpPage />} />
                    </Route>
                  </Routes>
                </ProjectProvider>
              </BrowserRouter>
            </AuthGate>
          </TooltipProvider>
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}

export default App;
