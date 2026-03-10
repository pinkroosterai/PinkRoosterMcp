import { useState } from "react";
import {
  type ColumnDef,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from "@tanstack/react-table";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useActivityLogs } from "@/hooks/use-activity-logs";
import type { ActivityLog } from "@/types";

const columns: ColumnDef<ActivityLog>[] = [
  {
    accessorKey: "timestamp",
    header: "Time",
    cell: ({ row }) => {
      const date = new Date(row.getValue("timestamp") as string);
      return (
        <span className="whitespace-nowrap text-sm">
          {date.toLocaleDateString()}{" "}
          {date.toLocaleTimeString()}
        </span>
      );
    },
  },
  {
    accessorKey: "httpMethod",
    header: "Method",
    cell: ({ row }) => {
      const method = row.getValue("httpMethod") as string;
      const variant =
        method === "GET"
          ? "secondary"
          : method === "POST"
            ? "default"
            : method === "DELETE"
              ? "destructive"
              : "outline";
      return <Badge variant={variant}>{method}</Badge>;
    },
  },
  {
    accessorKey: "path",
    header: "Path",
  },
  {
    accessorKey: "statusCode",
    header: "Status",
    cell: ({ row }) => {
      const code = row.getValue("statusCode") as number;
      const color =
        code < 300
          ? "text-green-600"
          : code < 400
            ? "text-yellow-600"
            : "text-red-600";
      return <span className={`font-mono font-medium ${color}`}>{code}</span>;
    },
  },
  {
    accessorKey: "durationMs",
    header: "Duration",
    cell: ({ row }) => (
      <span className="font-mono text-sm">
        {row.getValue("durationMs") as number}ms
      </span>
    ),
  },
  {
    accessorKey: "callerIdentity",
    header: "Caller",
    cell: ({ row }) => {
      const caller = row.getValue("callerIdentity") as string | null;
      return (
        <span className="text-sm text-muted-foreground">
          {caller ?? "anonymous"}
        </span>
      );
    },
  },
];

export function ActivityLogPage() {
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const { data, isLoading } = useActivityLogs(page, pageSize);

  const table = useReactTable({
    data: data?.items ?? [],
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">Activity Log</h1>

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableHead key={header.id}>
                    {header.isPlaceholder
                      ? null
                      : flexRender(
                          header.column.columnDef.header,
                          header.getContext(),
                        )}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {isLoading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <TableRow key={i}>
                  {columns.map((_, j) => (
                    <TableCell key={j}>
                      <Skeleton className="h-4 w-full" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : table.getRowModel().rows.length ? (
              table.getRowModel().rows.map((row) => (
                <TableRow key={row.id}>
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id}>
                      {flexRender(
                        cell.column.columnDef.cell,
                        cell.getContext(),
                      )}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-24 text-center"
                >
                  No activity logged yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      {data && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Showing {data.items.length} of {data.totalCount} entries (page{" "}
            {data.page} of {data.totalPages})
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={!data.hasPreviousPage}
              onClick={() => setPage((p) => p - 1)}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={!data.hasNextPage}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
