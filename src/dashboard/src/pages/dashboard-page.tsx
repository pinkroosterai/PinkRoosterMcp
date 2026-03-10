import { Link, useNavigate } from "react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useActivityLogs } from "@/hooks/use-activity-logs";
import { useHealth } from "@/hooks/use-health";
import { useProjectContext } from "@/hooks/use-project-context";
import { useProjectStatus, useNextActions } from "@/hooks/use-projects";
import { ScrollText, Activity, Clock, FolderOpen, Bug, Layers, Lightbulb, ArrowRight } from "lucide-react";

const priorityVariant: Record<string, "destructive" | "default" | "secondary" | "outline"> = {
  Critical: "destructive",
  High: "default",
  Medium: "secondary",
  Low: "outline",
};

const typeIcons: Record<string, string> = {
  Task: "T",
  WorkPackage: "WP",
  Issue: "I",
  FeatureRequest: "FR",
};

function getDetailPath(projectId: number, type: string, id: string): string {
  if (type === "Task" || type === "WorkPackage") {
    const wpNum = id.split("-wp-")[1]?.split("-")[0];
    return `/projects/${projectId}/work-packages/${wpNum}`;
  }
  if (type === "Issue") {
    const issueNum = id.split("-issue-")[1];
    return `/projects/${projectId}/issues/${issueNum}`;
  }
  if (type === "FeatureRequest") {
    const frNum = id.split("-fr-")[1];
    return `/projects/${projectId}/feature-requests/${frNum}`;
  }
  return `/projects/${projectId}`;
}

export function DashboardPage() {
  const { selectedProject } = useProjectContext();
  const navigate = useNavigate();
  const { data } = useActivityLogs(1, 1);
  const { data: isHealthy, isLoading: healthLoading } = useHealth();
  const { data: projectStatus } = useProjectStatus(selectedProject?.id);
  const { data: nextActions } = useNextActions(selectedProject?.id, 10);

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

      {/* Project Status Summary */}
      {projectStatus && (
        <div className="grid gap-4 md:grid-cols-3">
          <Card
            className="cursor-pointer hover:bg-muted/50 transition-colors"
            onClick={() => navigate(`/projects/${selectedProject.id}`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Issues</CardTitle>
              <Bug className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{projectStatus.issues.active} active</div>
              <p className="text-xs text-muted-foreground">
                {projectStatus.issues.terminal} completed &middot; {projectStatus.issues.percentComplete}% done
              </p>
            </CardContent>
          </Card>

          <Card
            className="cursor-pointer hover:bg-muted/50 transition-colors"
            onClick={() => navigate(`/projects/${selectedProject.id}`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Feature Requests</CardTitle>
              <Lightbulb className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{projectStatus.featureRequests.active} active</div>
              <p className="text-xs text-muted-foreground">
                {projectStatus.featureRequests.terminal} completed &middot; {projectStatus.featureRequests.percentComplete}% done
              </p>
            </CardContent>
          </Card>

          <Card
            className="cursor-pointer hover:bg-muted/50 transition-colors"
            onClick={() => navigate(`/projects/${selectedProject.id}`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Work Packages</CardTitle>
              <Layers className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{projectStatus.workPackages.active.length} active</div>
              <p className="text-xs text-muted-foreground">
                {projectStatus.workPackages.blocked.length} blocked &middot; {projectStatus.workPackages.percentComplete}% done
              </p>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Next Actions */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between pb-2">
          <CardTitle className="text-sm font-medium">Next Actions</CardTitle>
          <ArrowRight className="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          {!nextActions?.length ? (
            <p className="text-sm text-muted-foreground">No actionable items.</p>
          ) : (
            <div className="space-y-2">
              {nextActions.map((item) => (
                <div
                  key={item.id}
                  className="flex items-center gap-2 p-2 rounded-md hover:bg-muted/50 cursor-pointer transition-colors"
                  onClick={() => navigate(getDetailPath(selectedProject.id, item.type, item.id))}
                >
                  <Badge variant={priorityVariant[item.priority] ?? "outline"} className="text-xs">
                    {item.priority}
                  </Badge>
                  <Badge variant="outline" className="text-xs">
                    {typeIcons[item.type] ?? item.type}
                  </Badge>
                  <span className="text-sm font-medium truncate flex-1">{item.name}</span>
                  <span className="text-xs text-muted-foreground">{item.state}</span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
