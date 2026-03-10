import { BrowserRouter, Routes, Route } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ProjectProvider } from "@/hooks/use-project-context";
import { AppLayout } from "@/components/layout/app-layout";
import { DashboardPage } from "@/pages/dashboard-page";
import { ActivityLogPage } from "@/pages/activity-log-page";
import { ProjectListPage } from "@/pages/project-list-page";
import { ProjectDetailPage } from "@/pages/project-detail-page";
import { IssueDetailPage } from "@/pages/issue-detail-page";
import { WorkPackageDetailPage } from "@/pages/work-package-detail-page";
import { FeatureRequestDetailPage } from "@/pages/feature-request-detail-page";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
});

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <BrowserRouter>
          <ProjectProvider>
            <Routes>
              <Route element={<AppLayout />}>
                <Route index element={<DashboardPage />} />
                <Route path="projects" element={<ProjectListPage />} />
                <Route path="projects/:id" element={<ProjectDetailPage />} />
                <Route path="projects/:id/issues/:issueNumber" element={<IssueDetailPage />} />
                <Route path="projects/:id/work-packages/:wpNumber" element={<WorkPackageDetailPage />} />
                <Route path="projects/:id/feature-requests/:featureNumber" element={<FeatureRequestDetailPage />} />
                <Route path="activity" element={<ActivityLogPage />} />
              </Route>
            </Routes>
          </ProjectProvider>
        </BrowserRouter>
      </TooltipProvider>
    </QueryClientProvider>
  );
}

export default App;
