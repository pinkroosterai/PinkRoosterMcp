import { Link, useNavigate } from "react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { useActivityLogs } from "@/hooks/use-activity-logs";
import { useHealth } from "@/hooks/use-health";
import { useProjectContext } from "@/hooks/use-project-context";
import { useProjectStatus, useNextActions } from "@/hooks/use-projects";
import { stateColorClass, priorityAccent } from "@/lib/state-colors";
import { ScrollText, Activity, Clock, FolderOpen, Bug, Layers, Lightbulb, ArrowRight } from "lucide-react";
import { PieChart, Pie, Cell, ResponsiveContainer } from "recharts";

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

function MiniDonut({ percent, size = 48 }: { percent: number; size?: number }) {
  const data = [
    { value: percent },
    { value: 100 - percent },
  ];
  return (
    <div style={{ width: size, height: size }} className="relative">
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie
            data={data}
            innerRadius={size * 0.32}
            outerRadius={size * 0.46}
            startAngle={90}
            endAngle={-270}
            dataKey="value"
            stroke="none"
          >
            <Cell fill="hsl(350, 80%, 55%)" />
            <Cell fill="hsl(224, 15%, 18%)" className="dark:opacity-100 opacity-20" />
          </Pie>
        </PieChart>
      </ResponsiveContainer>
      <span className="absolute inset-0 flex items-center justify-center text-xs font-bold">
        {percent}%
      </span>
    </div>
  );
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

      {/* Metric cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card className="border-l-4 border-l-primary/50">
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

        <Card className={`border-l-4 ${healthLoading ? "border-l-muted" : isHealthy ? "border-l-emerald-500" : "border-l-red-500"}`}>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Status</CardTitle>
            <Activity className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className={`text-2xl font-bold ${healthLoading ? "text-muted-foreground" : isHealthy ? "text-emerald-500" : "text-red-500"}`}>
              {healthLoading ? "Checking..." : isHealthy ? "Online" : "Offline"}
            </div>
            <p className="text-xs text-muted-foreground">
              API server status
            </p>
          </CardContent>
        </Card>

        <Card className="border-l-4 border-l-blue-500/50">
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

      {/* Entity summary cards with progress */}
      {projectStatus && (
        <div className="grid gap-4 md:grid-cols-3">
          <Card
            className="cursor-pointer hover:bg-accent/50 transition-colors"
            onClick={() => navigate(`/projects/${selectedProject.id}`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Issues</CardTitle>
              <Bug className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-2xl font-bold">{projectStatus.issues.active} active</div>
                  <p className="text-xs text-muted-foreground">
                    {projectStatus.issues.terminal} completed
                  </p>
                </div>
                <MiniDonut percent={projectStatus.issues.percentComplete} />
              </div>
              <Progress
                value={projectStatus.issues.percentComplete}
                indicatorClassName="bg-emerald-500"
              />
            </CardContent>
          </Card>

          <Card
            className="cursor-pointer hover:bg-accent/50 transition-colors"
            onClick={() => navigate(`/projects/${selectedProject.id}`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Feature Requests</CardTitle>
              <Lightbulb className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-2xl font-bold">{projectStatus.featureRequests.active} active</div>
                  <p className="text-xs text-muted-foreground">
                    {projectStatus.featureRequests.terminal} completed
                  </p>
                </div>
                <MiniDonut percent={projectStatus.featureRequests.percentComplete} />
              </div>
              <Progress
                value={projectStatus.featureRequests.percentComplete}
                indicatorClassName="bg-blue-500"
              />
            </CardContent>
          </Card>

          <Card
            className="cursor-pointer hover:bg-accent/50 transition-colors"
            onClick={() => navigate(`/projects/${selectedProject.id}`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Work Packages</CardTitle>
              <Layers className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-2xl font-bold">{projectStatus.workPackages.active.length} active</div>
                  <p className="text-xs text-muted-foreground">
                    {projectStatus.workPackages.blocked.length} blocked
                  </p>
                </div>
                <MiniDonut percent={projectStatus.workPackages.percentComplete} />
              </div>
              <Progress
                value={projectStatus.workPackages.percentComplete}
                indicatorClassName="bg-purple-500"
              />
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
            <div className="space-y-1">
              {nextActions.map((item) => (
                <div
                  key={item.id}
                  className={`flex items-center gap-3 p-2.5 rounded-lg hover:bg-accent/50 cursor-pointer transition-colors border-l-3 ${priorityAccent[item.priority] ?? "border-l-transparent"}`}
                  onClick={() => navigate(getDetailPath(selectedProject.id, item.type, item.id))}
                >
                  <Badge variant={priorityVariant[item.priority] ?? "outline"} className="text-xs shrink-0">
                    {item.priority}
                  </Badge>
                  <Badge variant="outline" className="text-xs shrink-0">
                    {typeIcons[item.type] ?? item.type}
                  </Badge>
                  <span className="text-sm font-medium truncate flex-1">{item.name}</span>
                  <span className={stateColorClass(item.state)}>
                    {item.state}
                  </span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
