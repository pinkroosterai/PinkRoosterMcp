import { useState } from "react";
import { useParams, useNavigate } from "react-router";
import { ArrowLeft, Trash2, Bug, Layers } from "lucide-react";
import { useProjects } from "@/hooks/use-projects";
import { useIssues, useIssueSummary, useDeleteIssue } from "@/hooks/use-issues";
import { useWorkPackages, useWorkPackageSummary, useDeleteWorkPackage } from "@/hooks/use-work-packages";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import type { Issue, WorkPackage } from "@/types";

const stateFilters = [
  { label: "All", value: undefined },
  { label: "Active", value: "active" },
  { label: "Inactive", value: "inactive" },
  { label: "Terminal", value: "terminal" },
] as const;

const severityVariant: Record<string, "destructive" | "default" | "secondary" | "outline"> = {
  Critical: "destructive",
  Major: "default",
  Minor: "secondary",
  Trivial: "outline",
};

const stateColors: Record<string, string> = {
  NotStarted: "bg-gray-100 text-gray-700",
  Designing: "bg-blue-100 text-blue-700",
  Implementing: "bg-indigo-100 text-indigo-700",
  Testing: "bg-yellow-100 text-yellow-700",
  InReview: "bg-purple-100 text-purple-700",
  Completed: "bg-green-100 text-green-700",
  Cancelled: "bg-red-100 text-red-700",
  Blocked: "bg-orange-100 text-orange-700",
  Replaced: "bg-gray-200 text-gray-600",
};

const typeVariant: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
  Feature: "default",
  BugFix: "destructive",
  Refactor: "secondary",
  Spike: "outline",
  Chore: "outline",
};

function computeProgress(wp: WorkPackage): { completed: number; total: number } {
  let completed = 0;
  let total = 0;
  for (const phase of wp.phases) {
    for (const task of phase.tasks) {
      total++;
      if (task.state === "Completed") {
        completed++;
      }
    }
  }
  return { completed, total };
}

export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const navigate = useNavigate();
  const { data: projects, isLoading: projectsLoading } = useProjects();
  const project = projects?.find((p) => p.id === projectId);

  const [activeTab, setActiveTab] = useState<"issues" | "work-packages">("issues");

  const [stateFilter, setStateFilter] = useState<string | undefined>(undefined);
  const { data: issues, isLoading: issuesLoading } = useIssues(projectId, stateFilter);
  const { data: summary } = useIssueSummary(projectId);
  const deleteIssue = useDeleteIssue();
  const [issueToDelete, setIssueToDelete] = useState<Issue | null>(null);

  const [wpStateFilter, setWpStateFilter] = useState<string | undefined>(undefined);
  const { data: workPackages, isLoading: wpLoading } = useWorkPackages(projectId, wpStateFilter);
  const { data: wpSummary } = useWorkPackageSummary(projectId);
  const deleteWp = useDeleteWorkPackage();
  const [wpToDelete, setWpToDelete] = useState<WorkPackage | null>(null);

  const handleDelete = () => {
    if (!issueToDelete) return;
    deleteIssue.mutate(
      { projectId, issueNumber: issueToDelete.issueNumber },
      { onSettled: () => setIssueToDelete(null) },
    );
  };

  const handleWpDelete = () => {
    if (!wpToDelete) return;
    deleteWp.mutate(
      { projectId, wpNumber: wpToDelete.workPackageNumber },
      { onSettled: () => setWpToDelete(null) },
    );
  };

  if (projectsLoading) {
    return (
      <div className="space-y-6">
        <div className="text-muted-foreground">Loading...</div>
      </div>
    );
  }

  if (!project) {
    return (
      <div className="space-y-6">
        <Button variant="ghost" size="sm" onClick={() => navigate("/projects")}>
          <ArrowLeft className="size-4 mr-1" /> Back to projects
        </Button>
        <div className="text-muted-foreground">Project not found.</div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => navigate("/projects")}>
          <ArrowLeft className="size-4" />
        </Button>
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-bold">{project.name}</h1>
            <Badge variant="outline">{project.projectId}</Badge>
            <Badge variant={project.status === "Active" ? "default" : "secondary"}>
              {project.status}
            </Badge>
          </div>
          <p className="text-sm text-muted-foreground mt-1">{project.description}</p>
        </div>
      </div>

      <div className="flex items-center gap-2 border-b pb-2">
        <Button
          variant={activeTab === "issues" ? "default" : "outline"}
          size="sm"
          onClick={() => setActiveTab("issues")}
        >
          <Bug className="size-4 mr-1" />
          Issues
        </Button>
        <Button
          variant={activeTab === "work-packages" ? "default" : "outline"}
          size="sm"
          onClick={() => setActiveTab("work-packages")}
        >
          <Layers className="size-4 mr-1" />
          Work Packages
        </Button>
      </div>

      {activeTab === "issues" && (
        <>
          {summary && (
            <div className="grid grid-cols-3 gap-4">
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium text-muted-foreground">Active</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{summary.activeCount}</div>
                </CardContent>
              </Card>
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium text-muted-foreground">Inactive</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{summary.inactiveCount}</div>
                </CardContent>
              </Card>
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium text-muted-foreground">Terminal</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{summary.terminalCount}</div>
                </CardContent>
              </Card>
            </div>
          )}

          <div className="flex items-center gap-2">
            {stateFilters.map((f) => (
              <Button
                key={f.label}
                variant={stateFilter === f.value ? "default" : "outline"}
                size="sm"
                onClick={() => setStateFilter(f.value)}
              >
                {f.label}
              </Button>
            ))}
          </div>

          {issuesLoading ? (
            <div className="text-muted-foreground">Loading issues...</div>
          ) : !issues?.length ? (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-12 text-center">
                <Bug className="size-12 text-muted-foreground mb-4" />
                <h2 className="text-lg font-semibold">No issues found</h2>
                <p className="text-sm text-muted-foreground mt-1 max-w-sm">
                  Issues are created by AI agents via MCP tools.
                </p>
              </CardContent>
            </Card>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Issue ID</TableHead>
                    <TableHead>Name</TableHead>
                    <TableHead className="hidden md:table-cell">Type</TableHead>
                    <TableHead>Severity</TableHead>
                    <TableHead className="hidden sm:table-cell">Priority</TableHead>
                    <TableHead>State</TableHead>
                    <TableHead className="hidden lg:table-cell">Created</TableHead>
                    <TableHead className="w-[60px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {issues.map((issue) => (
                    <TableRow
                      key={issue.id}
                      className="cursor-pointer"
                      onClick={() => navigate(`/projects/${projectId}/issues/${issue.issueNumber}`)}
                    >
                      <TableCell>
                        <Badge variant="outline" className="text-xs">
                          {issue.issueId}
                        </Badge>
                      </TableCell>
                      <TableCell className="font-medium max-w-[200px] truncate">
                        {issue.name}
                      </TableCell>
                      <TableCell className="hidden md:table-cell text-muted-foreground">
                        {issue.issueType}
                      </TableCell>
                      <TableCell>
                        <Badge variant={severityVariant[issue.severity] ?? "outline"}>
                          {issue.severity}
                        </Badge>
                      </TableCell>
                      <TableCell className="hidden sm:table-cell">{issue.priority}</TableCell>
                      <TableCell>
                        <span
                          className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ${stateColors[issue.state] ?? ""}`}
                        >
                          {issue.state}
                        </span>
                      </TableCell>
                      <TableCell className="hidden lg:table-cell text-muted-foreground">
                        {new Date(issue.createdAt).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <Button
                          variant="ghost"
                          size="icon-xs"
                          onClick={(e) => {
                            e.stopPropagation();
                            setIssueToDelete(issue);
                          }}
                        >
                          <Trash2 className="size-3.5" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}

          <AlertDialog
            open={!!issueToDelete}
            onOpenChange={(open) => !open && setIssueToDelete(null)}
          >
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete issue?</AlertDialogTitle>
                <AlertDialogDescription>
                  This will permanently delete{" "}
                  <strong>{issueToDelete?.name}</strong> ({issueToDelete?.issueId}).
                  This action cannot be undone.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction
                  onClick={handleDelete}
                  className="bg-destructive text-white hover:bg-destructive/90"
                >
                  Delete
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </>
      )}

      {activeTab === "work-packages" && (
        <>
          {wpSummary && (
            <div className="grid grid-cols-3 gap-4">
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium text-muted-foreground">Active WPs</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{wpSummary.activeCount}</div>
                </CardContent>
              </Card>
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium text-muted-foreground">Inactive WPs</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{wpSummary.inactiveCount}</div>
                </CardContent>
              </Card>
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium text-muted-foreground">Terminal WPs</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{wpSummary.terminalCount}</div>
                </CardContent>
              </Card>
            </div>
          )}

          <div className="flex items-center gap-2">
            {stateFilters.map((f) => (
              <Button
                key={f.label}
                variant={wpStateFilter === f.value ? "default" : "outline"}
                size="sm"
                onClick={() => setWpStateFilter(f.value)}
              >
                {f.label}
              </Button>
            ))}
          </div>

          {wpLoading ? (
            <div className="text-muted-foreground">Loading work packages...</div>
          ) : !workPackages?.length ? (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-12 text-center">
                <Layers className="size-12 text-muted-foreground mb-4" />
                <h2 className="text-lg font-semibold">No work packages found</h2>
                <p className="text-sm text-muted-foreground mt-1 max-w-sm">
                  Work packages are created by AI agents via MCP tools.
                </p>
              </CardContent>
            </Card>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>WP ID</TableHead>
                    <TableHead>Name</TableHead>
                    <TableHead className="hidden md:table-cell">Type</TableHead>
                    <TableHead className="hidden sm:table-cell">Priority</TableHead>
                    <TableHead>State</TableHead>
                    <TableHead>Progress</TableHead>
                    <TableHead className="hidden lg:table-cell">Created</TableHead>
                    <TableHead className="w-[60px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {workPackages.map((wp) => {
                    const progress = computeProgress(wp);
                    return (
                      <TableRow
                        key={wp.id}
                        className="cursor-pointer"
                        onClick={() => navigate(`/projects/${projectId}/work-packages/${wp.workPackageNumber}`)}
                      >
                        <TableCell>
                          <Badge variant="outline" className="text-xs">
                            {wp.workPackageId}
                          </Badge>
                        </TableCell>
                        <TableCell className="font-medium max-w-[200px] truncate">
                          {wp.name}
                        </TableCell>
                        <TableCell className="hidden md:table-cell">
                          <Badge variant={typeVariant[wp.type] ?? "outline"}>
                            {wp.type}
                          </Badge>
                        </TableCell>
                        <TableCell className="hidden sm:table-cell">{wp.priority}</TableCell>
                        <TableCell>
                          <span
                            className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ${stateColors[wp.state] ?? ""}`}
                          >
                            {wp.state}
                          </span>
                        </TableCell>
                        <TableCell className="text-muted-foreground text-sm">
                          {progress.completed}/{progress.total} tasks
                        </TableCell>
                        <TableCell className="hidden lg:table-cell text-muted-foreground">
                          {new Date(wp.createdAt).toLocaleDateString()}
                        </TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="icon-xs"
                            onClick={(e) => {
                              e.stopPropagation();
                              setWpToDelete(wp);
                            }}
                          >
                            <Trash2 className="size-3.5" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}

          <AlertDialog
            open={!!wpToDelete}
            onOpenChange={(open) => !open && setWpToDelete(null)}
          >
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete work package?</AlertDialogTitle>
                <AlertDialogDescription>
                  This will permanently delete{" "}
                  <strong>{wpToDelete?.name}</strong> ({wpToDelete?.workPackageId}).
                  This action cannot be undone.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction
                  onClick={handleWpDelete}
                  className="bg-destructive text-white hover:bg-destructive/90"
                >
                  Delete
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </>
      )}
    </div>
  );
}
