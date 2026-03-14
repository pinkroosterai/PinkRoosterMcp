import { useState } from "react";
import { useParams, useNavigate, useSearchParams, Link } from "react-router";
import { type ColumnDef } from "@tanstack/react-table";
import { Trash2, Lightbulb, Plus } from "lucide-react";
import { useFeatureRequests, useDeleteFeatureRequest } from "@/hooks/use-feature-requests";
import { usePermissions } from "@/hooks/use-permissions";
import { PageTransition } from "@/components/page-transition";
import { TableSkeleton } from "@/components/loading-skeletons";
import { useRowHighlight } from "@/hooks/use-row-highlight";
import { Badge } from "@/components/ui/badge";
import { AnimatedBadge } from "@/components/animated-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
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
import { stateColorClass } from "@/lib/state-colors";
import type { FeatureRequest } from "@/types";

const stateFilters = [
  { label: "Open", value: "open" },
  { label: "All", value: "all" },
  { label: "Active", value: "active" },
  { label: "Inactive", value: "inactive" },
  { label: "Terminal", value: "terminal" },
] as const;

const columnFilters: ColumnFilterConfig[] = [
  { columnId: "name", type: "text", placeholder: "Filter name..." },
  {
    columnId: "category",
    type: "select",
    placeholder: "Category",
    options: [
      { label: "Feature", value: "Feature" },
      { label: "Enhancement", value: "Enhancement" },
      { label: "Improvement", value: "Improvement" },
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

export function FeatureRequestsListPage() {
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const navigate = useNavigate();

  const [searchParams, setSearchParams] = useSearchParams();
  const stateFilter = searchParams.get("state") ?? "open";
  const setStateFilter = (value: string) => {
    setSearchParams({ state: value }, { replace: true });
  };
  const apiFilter = stateFilter === "all" ? undefined : stateFilter;
  const { data: featureRequests, isLoading } = useFeatureRequests(projectId, apiFilter);
  const { canCreate } = usePermissions(projectId);
  const deleteFr = useDeleteFeatureRequest();
  const [frToDelete, setFrToDelete] = useState<FeatureRequest | null>(null);
  const { rowClassName } = useRowHighlight(featureRequests ?? [], (fr) => fr.featureRequestId);

  const handleDelete = () => {
    if (!frToDelete) return;
    deleteFr.mutate(
      { projectId, frNumber: frToDelete.featureRequestNumber },
      { onSettled: () => setFrToDelete(null) },
    );
  };

  const columns: ColumnDef<FeatureRequest>[] = [
    {
      accessorKey: "featureRequestId",
      header: "FR ID",
      enableSorting: false,
      cell: ({ row }) => (
        <Badge variant="outline" className="text-xs">{row.getValue("featureRequestId")}</Badge>
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
      accessorKey: "category",
      header: "Category",
      meta: { className: "hidden md:table-cell" },
      cell: ({ row }) => (
        <span className="text-muted-foreground">{row.getValue("category")}</span>
      ),
    },
    {
      accessorKey: "priority",
      header: "Priority",
      meta: { className: "hidden sm:table-cell" },
      cell: ({ row }) => <span>{row.getValue("priority")}</span>,
    },
    {
      accessorKey: "status",
      header: "Status",
      enableSorting: false,
      cell: ({ row }) => (
        <AnimatedBadge value={row.getValue("status")} className={stateColorClass(row.getValue("status"), "feature")}>{row.getValue("status")}</AnimatedBadge>
      ),
    },
    {
      accessorKey: "requester",
      header: "Requester",
      enableSorting: false,
      meta: { className: "hidden lg:table-cell" },
      cell: ({ row }) => (
        <span className="text-muted-foreground">{row.getValue("requester") ?? "\u2014"}</span>
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
            setFrToDelete(row.original);
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
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2 animate-in-right">
          <Lightbulb className="size-6" /> Feature Requests
        </h1>
        {canCreate && (
          <Button asChild>
            <Link to={`/projects/${projectId}/feature-requests/new`}>
              <Plus className="size-4 mr-1.5" /> Create Feature Request
            </Link>
          </Button>
        )}
      </div>

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
      ) : !featureRequests?.length ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <Lightbulb className="size-12 text-muted-foreground mb-4" />
            <h2 className="text-lg font-semibold">No feature requests found</h2>
            <p className="text-sm text-muted-foreground mt-1 mb-4 max-w-sm">
              Create your first feature request to start tracking ideas.
            </p>
            {canCreate && (
              <Button asChild>
                <Link to={`/projects/${projectId}/feature-requests/new`}>
                  <Plus className="size-4 mr-1.5" /> Create Feature Request
                </Link>
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <DataTable
          columns={columns}
          data={featureRequests}
          filters={columnFilters}
          onRowClick={(fr) => navigate(`/projects/${projectId}/feature-requests/${fr.featureRequestNumber}`)}
          rowClassName={rowClassName}
          emptyMessage="No feature requests match the current filters."
        />
      )}

      <AlertDialog
        open={!!frToDelete}
        onOpenChange={(open) => !open && setFrToDelete(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete feature request?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete{" "}
              <strong>{frToDelete?.name}</strong> ({frToDelete?.featureRequestId}).
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
