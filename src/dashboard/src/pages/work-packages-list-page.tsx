import { useState } from "react";
import { useParams, useNavigate, useSearchParams, Link } from "react-router";
import { type ColumnDef } from "@tanstack/react-table";
import { Trash2, Layers, Plus } from "lucide-react";
import { PieChart, Pie, Cell, ResponsiveContainer } from "recharts";
import { useWorkPackages, useWorkPackageSummary, useDeleteWorkPackage } from "@/hooks/use-work-packages";
import { usePermissions } from "@/hooks/use-permissions";
import { PageTransition } from "@/components/page-transition";
import { TableSkeleton } from "@/components/loading-skeletons";
import { useRowHighlight } from "@/hooks/use-row-highlight";
import { Badge } from "@/components/ui/badge";
import { AnimatedBadge } from "@/components/animated-badge";
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
import { Progress } from "@/components/ui/progress";
import { DataTable, type ColumnFilterConfig } from "@/components/data-table";
import { AnimatedCount } from "@/components/animated-count";
import { stateColorClass } from "@/lib/state-colors";
import type { WorkPackage } from "@/types";

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
  { label: "Open", value: "open" },
  { label: "All", value: "all" },
  { label: "Active", value: "active" },
  { label: "Inactive", value: "inactive" },
  { label: "Terminal", value: "terminal" },
] as const;

const typeVariant: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
  Feature: "default",
  BugFix: "destructive",
  Refactor: "secondary",
  Spike: "outline",
  Chore: "outline",
};

function computeProgress(wp: WorkPackage): { completed: number; total: number; percent: number } {
  const total = wp.taskCount;
  const completed = wp.completedTaskCount;
  return { completed, total, percent: total > 0 ? (completed / total) * 100 : 0 };
}

const columnFilters: ColumnFilterConfig[] = [
  { columnId: "name", type: "text", placeholder: "Filter name..." },
  {
    columnId: "type",
    type: "select",
    placeholder: "Type",
    options: [
      { label: "Feature", value: "Feature" },
      { label: "BugFix", value: "BugFix" },
      { label: "Refactor", value: "Refactor" },
      { label: "Spike", value: "Spike" },
      { label: "Chore", value: "Chore" },
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

export function WorkPackagesListPage() {
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const navigate = useNavigate();

  const [searchParams, setSearchParams] = useSearchParams();
  const stateFilter = searchParams.get("state") ?? "open";
  const setStateFilter = (value: string) => {
    setSearchParams({ state: value }, { replace: true });
  };
  const apiFilter = stateFilter === "all" ? undefined : stateFilter;
  const { data: workPackages, isLoading } = useWorkPackages(projectId, apiFilter);
  const { data: summary } = useWorkPackageSummary(projectId);
  const { canCreate } = usePermissions(projectId);
  const deleteWp = useDeleteWorkPackage();
  const [wpToDelete, setWpToDelete] = useState<WorkPackage | null>(null);
  const { rowClassName } = useRowHighlight(workPackages ?? [], (wp) => wp.workPackageId);

  const handleDelete = () => {
    if (!wpToDelete) return;
    deleteWp.mutate(
      { projectId, wpNumber: wpToDelete.workPackageNumber },
      { onSettled: () => setWpToDelete(null) },
    );
  };

  const columns: ColumnDef<WorkPackage>[] = [
    {
      accessorKey: "workPackageId",
      header: "WP ID",
      enableSorting: false,
      cell: ({ row }) => (
        <Badge variant="outline" className="text-xs">{row.getValue("workPackageId")}</Badge>
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
      accessorKey: "type",
      header: "Type",
      meta: { className: "hidden md:table-cell" },
      cell: ({ row }) => {
        const t = row.getValue("type") as string;
        return <Badge variant={typeVariant[t] ?? "outline"}>{t}</Badge>;
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
        <AnimatedBadge value={row.getValue("state")} className={stateColorClass(row.getValue("state"))}>{row.getValue("state")}</AnimatedBadge>
      ),
    },
    {
      id: "progress",
      header: "Progress",
      sortingFn: (rowA, rowB) => {
        const a = computeProgress(rowA.original);
        const b = computeProgress(rowB.original);
        return a.percent - b.percent;
      },
      cell: ({ row }) => {
        const p = computeProgress(row.original);
        return (
          <div className="flex items-center gap-2">
            <Progress
              value={p.percent}
              className="h-2 w-20"
              indicatorClassName="bg-emerald-500"
            />
            <span className="text-xs text-muted-foreground">{p.completed}/{p.total}</span>
          </div>
        );
      },
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
            setWpToDelete(row.original);
          }}
        >
          <Trash2 className="size-3.5" />
        </Button>
      ),
    },
  ];

  return (
    <PageTransition>
    <div className="space-y-6">
      <div className="flex items-center justify-between animate-in-right">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Layers className="size-6" /> Work Packages
        </h1>
        {canCreate && (
          <Button asChild>
            <Link to={`/projects/${projectId}/work-packages/new`}>
              <Plus className="size-4 mr-1.5" /> Create Work Package
            </Link>
          </Button>
        )}
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
                <CardTitle className="text-sm font-medium text-muted-foreground">Active WPs</CardTitle>
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
                <CardTitle className="text-sm font-medium text-muted-foreground">Inactive WPs</CardTitle>
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
                <CardTitle className="text-sm font-medium text-muted-foreground">Terminal WPs</CardTitle>
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
        <TableSkeleton rows={8} columns={6} />
      ) : !workPackages?.length ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <Layers className="size-12 text-muted-foreground mb-4" />
            <h2 className="text-lg font-semibold">No work packages found</h2>
            <p className="text-sm text-muted-foreground mt-1 mb-4 max-w-sm">
              No work packages yet. Create one to start planning.
            </p>
            {canCreate && (
              <Button asChild>
                <Link to={`/projects/${projectId}/work-packages/new`}>
                  <Plus className="size-4 mr-1.5" /> Create Work Package
                </Link>
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <DataTable
          columns={columns}
          data={workPackages}
          filters={columnFilters}
          onRowClick={(wp) => navigate(`/projects/${projectId}/work-packages/${wp.workPackageNumber}`)}
          rowClassName={rowClassName}
          emptyMessage="No work packages match the current filters."
        />
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
              onClick={handleDelete}
              className="bg-destructive text-white hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
    </PageTransition>
  );
}
