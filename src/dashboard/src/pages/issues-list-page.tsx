import { useState } from "react";
import { useParams, useNavigate, Link } from "react-router";
import { type ColumnDef } from "@tanstack/react-table";
import { Trash2, Bug, Plus } from "lucide-react";
import { PieChart, Pie, Cell, ResponsiveContainer } from "recharts";
import { useIssues, useIssueSummary, useDeleteIssue } from "@/hooks/use-issues";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
import { DataTable, type ColumnFilterConfig } from "@/components/data-table";
import { AnimatedCount } from "@/components/animated-count";
import { stateColorClass } from "@/lib/state-colors";
import type { Issue } from "@/types";

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

const columnFilters: ColumnFilterConfig[] = [
  { columnId: "name", type: "text", placeholder: "Filter name..." },
  {
    columnId: "severity",
    type: "select",
    placeholder: "Severity",
    options: [
      { label: "Critical", value: "Critical" },
      { label: "Major", value: "Major" },
      { label: "Minor", value: "Minor" },
      { label: "Trivial", value: "Trivial" },
    ],
  },
  {
    columnId: "priority",
    type: "select",
    placeholder: "Priority",
    options: [
      { label: "Critical", value: "Critical" },
      { label: "High", value: "High" },
      { label: "Medium", value: "Medium" },
      { label: "Low", value: "Low" },
    ],
  },
];

export function IssuesListPage() {
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const navigate = useNavigate();

  const [stateFilter, setStateFilter] = useState<string | undefined>(undefined);
  const { data: issues, isLoading } = useIssues(projectId, stateFilter);
  const { data: summary } = useIssueSummary(projectId);
  const deleteIssue = useDeleteIssue();
  const [issueToDelete, setIssueToDelete] = useState<Issue | null>(null);

  const handleDelete = () => {
    if (!issueToDelete) return;
    deleteIssue.mutate(
      { projectId, issueNumber: issueToDelete.issueNumber },
      { onSettled: () => setIssueToDelete(null) },
    );
  };

  const columns: ColumnDef<Issue>[] = [
    {
      accessorKey: "issueId",
      header: "Issue ID",
      enableSorting: false,
      cell: ({ row }) => (
        <Badge variant="outline" className="text-xs">{row.getValue("issueId")}</Badge>
      ),
    },
    {
      accessorKey: "name",
      header: "Name",
      cell: ({ row }) => (
        <span className="font-medium max-w-[200px] truncate block">{row.getValue("name")}</span>
      ),
    },
    {
      accessorKey: "issueType",
      header: "Type",
      enableSorting: false,
      meta: { className: "hidden md:table-cell" },
      cell: ({ row }) => (
        <span className="text-muted-foreground">{row.getValue("issueType")}</span>
      ),
    },
    {
      accessorKey: "severity",
      header: "Severity",
      cell: ({ row }) => {
        const sev = row.getValue("severity") as string;
        return <Badge variant={severityVariant[sev] ?? "outline"}>{sev}</Badge>;
      },
    },
    {
      accessorKey: "priority",
      header: "Priority",
      meta: { className: "hidden sm:table-cell" },
      cell: ({ row }) => <span>{row.getValue("priority")}</span>,
    },
    {
      accessorKey: "state",
      header: "State",
      enableSorting: false,
      cell: ({ row }) => (
        <span className={stateColorClass(row.getValue("state"))}>{row.getValue("state")}</span>
      ),
    },
    {
      accessorKey: "createdAt",
      header: "Created",
      meta: { className: "hidden lg:table-cell" },
      cell: ({ row }) => (
        <span className="text-muted-foreground">
          {new Date(row.getValue("createdAt") as string).toLocaleDateString()}
        </span>
      ),
    },
    {
      id: "actions",
      enableSorting: false,
      cell: ({ row }) => (
        <Button
          variant="ghost"
          size="icon-xs"
          onClick={(e) => {
            e.stopPropagation();
            setIssueToDelete(row.original);
          }}
        >
          <Trash2 className="size-3.5" />
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2 animate-in-right">
          <Bug className="size-6" /> Issues
        </h1>
        <Button asChild>
          <Link to={`/projects/${projectId}/issues/new`}>
            <Plus className="size-4 mr-1.5" /> Create Issue
          </Link>
        </Button>
      </div>

      {summary && (() => {
        const total = summary.activeCount + summary.inactiveCount + summary.terminalCount;
        const pctActive = total > 0 ? Math.round((summary.activeCount / total) * 100) : 0;
        const pctInactive = total > 0 ? Math.round((summary.inactiveCount / total) * 100) : 0;
        const pctTerminal = total > 0 ? Math.round((summary.terminalCount / total) * 100) : 0;
        return (
          <div className="grid grid-cols-3 gap-4 stagger-children">
            <Card className="glass-card accent-emerald">
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Active</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between">
                  <div className="text-2xl font-bold"><AnimatedCount value={summary.activeCount} /></div>
                  <MiniDonut percent={pctActive} />
                </div>
              </CardContent>
            </Card>
            <Card className="glass-card accent-blue">
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Inactive</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between">
                  <div className="text-2xl font-bold"><AnimatedCount value={summary.inactiveCount} /></div>
                  <MiniDonut percent={pctInactive} />
                </div>
              </CardContent>
            </Card>
            <Card className="glass-card accent-purple">
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Terminal</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between">
                  <div className="text-2xl font-bold"><AnimatedCount value={summary.terminalCount} /></div>
                  <MiniDonut percent={pctTerminal} />
                </div>
              </CardContent>
            </Card>
          </div>
        );
      })()}

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

      {isLoading ? (
        <div className="text-muted-foreground">Loading issues...</div>
      ) : !issues?.length ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <Bug className="size-12 text-muted-foreground mb-4" />
            <h2 className="text-lg font-semibold">No issues found</h2>
            <p className="text-sm text-muted-foreground mt-1 mb-4 max-w-sm">
              Create your first issue to start tracking bugs and defects.
            </p>
            <Button asChild>
              <Link to={`/projects/${projectId}/issues/new`}>
                <Plus className="size-4 mr-1.5" /> Create Issue
              </Link>
            </Button>
          </CardContent>
        </Card>
      ) : (
        <DataTable
          columns={columns}
          data={issues}
          filters={columnFilters}
          onRowClick={(issue) => navigate(`/projects/${projectId}/issues/${issue.issueNumber}`)}
          emptyMessage="No issues match the current filters."
        />
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
    </div>
  );
}
