import { Link, useNavigate } from "react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { AnimatedCount } from "@/components/animated-count";
import { useProjectContext } from "@/hooks/use-project-context";
import { useProjectStatus, useNextActions } from "@/hooks/use-projects";
import { stateColorClass, priorityAccent } from "@/lib/state-colors";
import { FolderOpen, Bug, Layers, Lightbulb, ArrowRight } from "lucide-react";
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
  const { data: projectStatus } = useProjectStatus(selectedProject?.id);
  const { data: nextActions } = useNextActions(selectedProject?.id, 10);

  if (!selectedProject) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <Card className="max-w-sm animate-in-scale">
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
      <div className="animate-in-right">
        <h1 className="text-2xl font-bold">{selectedProject.name}</h1>
        {selectedProject.description && (
          <p className="text-sm text-muted-foreground mt-1">{selectedProject.description}</p>
        )}
      </div>

      {/* Entity summary cards with progress — staggered entrance */}
      {projectStatus && (
        <div className="grid gap-4 md:grid-cols-3 stagger-children">
          <Card
            className="glass-card card-hover accent-emerald cursor-pointer"
            onClick={() => navigate(`/projects/${selectedProject.id}/issues`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Issues</CardTitle>
              <Bug className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-2xl font-bold">
                    <AnimatedCount value={projectStatus.issues.active} suffix=" active" />
                  </div>
                  <p className="text-xs text-muted-foreground">
                    <AnimatedCount value={projectStatus.issues.terminal} suffix=" completed" />
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
            className="glass-card card-hover accent-blue cursor-pointer"
            onClick={() => navigate(`/projects/${selectedProject.id}/feature-requests`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Feature Requests</CardTitle>
              <Lightbulb className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-2xl font-bold">
                    <AnimatedCount value={projectStatus.featureRequests.active} suffix=" active" />
                  </div>
                  <p className="text-xs text-muted-foreground">
                    <AnimatedCount value={projectStatus.featureRequests.terminal} suffix=" completed" />
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
            className="glass-card card-hover accent-purple cursor-pointer"
            onClick={() => navigate(`/projects/${selectedProject.id}/work-packages`)}
          >
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Work Packages</CardTitle>
              <Layers className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-2xl font-bold">
                    <AnimatedCount value={projectStatus.workPackages.active.length} suffix=" active" />
                  </div>
                  <p className="text-xs text-muted-foreground">
                    <AnimatedCount value={projectStatus.workPackages.blocked.length} suffix=" blocked" />
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

      {/* Next Actions — staggered row entrance */}
      <Card className="glass-card animate-in-up" style={{ animationDelay: "200ms" }}>
        <CardHeader className="flex flex-row items-center justify-between pb-2">
          <CardTitle className="text-sm font-medium">Next Actions</CardTitle>
          <ArrowRight className="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          {!nextActions?.length ? (
            <p className="text-sm text-muted-foreground">No actionable items.</p>
          ) : (
            <div className="space-y-1 stagger-children">
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
