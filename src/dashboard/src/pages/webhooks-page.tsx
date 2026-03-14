import { useState } from "react";
import { useParams } from "react-router";
import { type ColumnDef } from "@tanstack/react-table";
import { Webhook, CheckCircle, XCircle, Clock, Circle } from "lucide-react";
import { useWebhooks, useWebhookDeliveries } from "@/hooks/use-webhooks";
import { PageTransition } from "@/components/page-transition";
import { TableSkeleton } from "@/components/loading-skeletons";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DataTable } from "@/components/data-table";
import type { WebhookSubscription, WebhookDeliveryLog } from "@/api/webhooks";

const subscriptionColumns: ColumnDef<WebhookSubscription>[] = [
  {
    accessorKey: "url",
    header: "URL",
    cell: ({ row }) => (
      <span className="font-mono text-xs truncate max-w-[300px] block">
        {row.getValue("url") as string}
      </span>
    ),
  },
  {
    accessorKey: "isActive",
    header: "Status",
    cell: ({ row }) => {
      const active = row.getValue("isActive") as boolean;
      return (
        <Badge variant={active ? "default" : "secondary"}>
          {active ? "Active" : "Inactive"}
        </Badge>
      );
    },
  },
  {
    accessorKey: "eventFilters",
    header: "Filters",
    cell: ({ row }) => {
      const filters = row.original.eventFilters;
      if (filters.length === 0) return <span className="text-muted-foreground text-sm">All events</span>;
      return (
        <div className="flex flex-wrap gap-1">
          {filters.map((f, i) => (
            <Badge key={i} variant="outline" className="text-xs">
              {f.eventType}
            </Badge>
          ))}
        </div>
      );
    },
  },
  {
    accessorKey: "consecutiveFailures",
    header: "Health",
    cell: ({ row }) => {
      const failures = row.original.consecutiveFailures;
      if (failures === 0) return <CheckCircle className="h-4 w-4 text-green-500" />;
      if (failures < 3) return <Clock className="h-4 w-4 text-yellow-500" />;
      return <XCircle className="h-4 w-4 text-red-500" />;
    },
  },
  {
    accessorKey: "lastDeliveredAt",
    header: "Last Delivery",
    cell: ({ row }) => {
      const date = row.getValue("lastDeliveredAt") as string | null;
      if (!date) return <span className="text-muted-foreground text-sm">Never</span>;
      return <span className="text-sm">{new Date(date).toLocaleString()}</span>;
    },
  },
];

const deliveryColumns: ColumnDef<WebhookDeliveryLog>[] = [
  {
    accessorKey: "createdAt",
    header: "Time",
    cell: ({ row }) => (
      <span className="text-sm whitespace-nowrap">
        {new Date(row.getValue("createdAt") as string).toLocaleString()}
      </span>
    ),
  },
  {
    accessorKey: "eventType",
    header: "Event",
    cell: ({ row }) => (
      <Badge variant="outline" className="text-xs font-mono">
        {row.getValue("eventType") as string}
      </Badge>
    ),
  },
  {
    accessorKey: "entityId",
    header: "Entity",
    cell: ({ row }) => (
      <span className="font-mono text-xs">{row.getValue("entityId") as string}</span>
    ),
  },
  {
    accessorKey: "httpStatusCode",
    header: "Status",
    cell: ({ row }) => {
      const code = row.getValue("httpStatusCode") as number | null;
      if (code === null) return <span className="text-muted-foreground text-sm">--</span>;
      const color = code >= 200 && code < 300 ? "text-green-500" : "text-red-500";
      return <span className={`font-mono text-sm ${color}`}>{code}</span>;
    },
  },
  {
    accessorKey: "success",
    header: "Result",
    cell: ({ row }) => {
      const success = row.getValue("success") as boolean;
      return success
        ? <CheckCircle className="h-4 w-4 text-green-500" />
        : <XCircle className="h-4 w-4 text-red-500" />;
    },
  },
  {
    accessorKey: "durationMs",
    header: "Duration",
    cell: ({ row }) => (
      <span className="text-sm">{row.getValue("durationMs") as number}ms</span>
    ),
  },
  {
    accessorKey: "attemptNumber",
    header: "Attempt",
    cell: ({ row }) => (
      <span className="text-sm">#{row.getValue("attemptNumber") as number}</span>
    ),
  },
];

export function WebhooksPage() {
  const { id } = useParams();
  const projectId = id ? Number(id) : undefined;
  const [selectedSub, setSelectedSub] = useState<number | null>(null);

  const { data: webhooks, isLoading } = useWebhooks(projectId);
  const { data: deliveries, isLoading: deliveriesLoading } = useWebhookDeliveries(
    projectId,
    selectedSub ?? undefined,
  );

  if (isLoading) {
    return (
      <PageTransition>
        <div className="space-y-6 p-6">
          <TableSkeleton rows={3} />
        </div>
      </PageTransition>
    );
  }

  return (
    <PageTransition>
      <div className="space-y-6 p-6">
        <div className="flex items-center gap-3">
          <Webhook className="h-6 w-6 text-primary" />
          <h1 className="text-2xl font-bold tracking-tight">Webhooks</h1>
          {webhooks && (
            <Badge variant="secondary">{webhooks.length} subscription(s)</Badge>
          )}
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Subscriptions</CardTitle>
          </CardHeader>
          <CardContent>
            {webhooks && webhooks.length > 0 ? (
              <DataTable
                columns={subscriptionColumns}
                data={webhooks}
                onRowClick={(row) => setSelectedSub(row.id)}
              />
            ) : (
              <p className="text-muted-foreground text-sm py-4">
                No webhook subscriptions configured. Use the API or MCP tools to create one.
              </p>
            )}
          </CardContent>
        </Card>

        {selectedSub && (
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                Delivery Log
                <Badge variant="outline" className="font-mono text-xs">
                  Subscription #{selectedSub}
                </Badge>
              </CardTitle>
            </CardHeader>
            <CardContent>
              {deliveriesLoading ? (
                <TableSkeleton rows={5} />
              ) : deliveries && deliveries.length > 0 ? (
                <DataTable columns={deliveryColumns} data={deliveries} />
              ) : (
                <p className="text-muted-foreground text-sm py-4">
                  No deliveries yet for this subscription.
                </p>
              )}
            </CardContent>
          </Card>
        )}
      </div>
    </PageTransition>
  );
}
