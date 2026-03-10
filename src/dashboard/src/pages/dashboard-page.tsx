import { Link } from "react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useActivityLogs } from "@/hooks/use-activity-logs";
import { useHealth } from "@/hooks/use-health";
import { useProjectContext } from "@/hooks/use-project-context";
import { ScrollText, Activity, Clock, FolderOpen } from "lucide-react";

export function DashboardPage() {
  const { selectedProject } = useProjectContext();
  const { data } = useActivityLogs(1, 1);
  const { data: isHealthy, isLoading: healthLoading } = useHealth();

  if (!selectedProject) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <Card className="max-w-sm">
          <CardContent className="flex flex-col items-center py-12 text-center">
            <FolderOpen className="size-12 text-muted-foreground mb-4" />
            <h2 className="text-lg font-semibold">Select a project</h2>
            <p className="text-sm text-muted-foreground mt-1 mb-4">
              Choose a project to get started, or browse all registered projects.
            </p>
            <Button asChild>
              <Link to="/projects">View Projects</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">{selectedProject.name}</h1>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">
              Total Requests
            </CardTitle>
            <ScrollText className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {data?.totalCount ?? "-"}
            </div>
            <p className="text-xs text-muted-foreground">
              Logged API requests
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Status</CardTitle>
            <Activity className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className={`text-2xl font-bold ${healthLoading ? "text-muted-foreground" : isHealthy ? "text-green-600" : "text-red-600"}`}>
              {healthLoading ? "Checking..." : isHealthy ? "Online" : "Offline"}
            </div>
            <p className="text-xs text-muted-foreground">
              API server status
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">
              Latest Activity
            </CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {data?.items[0]
                ? new Date(data.items[0].timestamp).toLocaleTimeString()
                : "-"}
            </div>
            <p className="text-xs text-muted-foreground">
              Most recent request
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
