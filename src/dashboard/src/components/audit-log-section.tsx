import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

interface AuditLogEntry {
  changedAt: string;
  fieldName: string;
  oldValue: string | null;
  newValue: string | null;
  changedBy: string;
}

interface AuditLogSectionProps {
  entries: AuditLogEntry[] | undefined;
  isLoading: boolean;
  expanded: boolean;
  onExpandedChange: (expanded: boolean) => void;
}

export function AuditLogSection({ entries, isLoading, expanded, onExpandedChange }: AuditLogSectionProps) {
  return (
    <Card>
      <CardHeader className="cursor-pointer select-none" onClick={() => onExpandedChange(!expanded)}>
        <CardTitle className="text-base flex items-center gap-2.5">
          Audit Log
          <span className="text-xs text-muted-foreground font-normal">
            ({entries?.length ?? 0} entries) {expanded ? "\u25B2" : "\u25BC"}
          </span>
        </CardTitle>
      </CardHeader>
      {expanded && (
        <CardContent>
          {isLoading ? (
            <div className="text-muted-foreground text-sm">Loading audit log...</div>
          ) : !entries?.length ? (
            <div className="text-muted-foreground text-sm">No audit entries.</div>
          ) : (
            <div className="rounded-lg border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Timestamp</TableHead>
                    <TableHead>Field</TableHead>
                    <TableHead>Old Value</TableHead>
                    <TableHead>New Value</TableHead>
                    <TableHead>Changed By</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {entries.map((entry, idx) => (
                    <TableRow key={idx}>
                      <TableCell className="text-xs text-muted-foreground whitespace-nowrap">
                        {new Date(entry.changedAt).toLocaleString()}
                      </TableCell>
                      <TableCell className="font-mono text-xs">{entry.fieldName}</TableCell>
                      <TableCell className="text-xs max-w-[200px] truncate">{entry.oldValue ?? "\u2014"}</TableCell>
                      <TableCell className="text-xs max-w-[200px] truncate">{entry.newValue ?? "\u2014"}</TableCell>
                      <TableCell className="text-xs">{entry.changedBy}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      )}
    </Card>
  );
}
