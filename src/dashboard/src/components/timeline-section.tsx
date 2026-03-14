import { Clock } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { formatDate } from "@/lib/form-utils";

interface TimelineSectionProps {
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  updatedAt: string;
}

export function TimelineSection({ createdAt, startedAt, completedAt, resolvedAt, updatedAt }: TimelineSectionProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base flex items-center gap-2.5">
          <Clock className="size-4" /> Timeline
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-2 md:grid-cols-5 gap-6 text-sm">
          <div>
            <div className="text-sm text-muted-foreground mb-1">Created</div>
            <div>{formatDate(createdAt)}</div>
          </div>
          <div>
            <div className="text-sm text-muted-foreground mb-1">Started</div>
            <div>{formatDate(startedAt)}</div>
          </div>
          <div>
            <div className="text-sm text-muted-foreground mb-1">Completed</div>
            <div>{formatDate(completedAt)}</div>
          </div>
          <div>
            <div className="text-sm text-muted-foreground mb-1">Resolved</div>
            <div>{formatDate(resolvedAt)}</div>
          </div>
          <div>
            <div className="text-sm text-muted-foreground mb-1">Updated</div>
            <div>{formatDate(updatedAt)}</div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
