import { useState } from "react";
import { useParams, useNavigate } from "react-router";
import { ArrowLeft, Trash2, Paperclip, Clock, FileText, Shield, Package } from "lucide-react";
import { useIssue, useIssueAuditLog, useDeleteIssue } from "@/hooks/use-issues";
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

function formatDate(value: string | null): string {
  if (!value) return "\u2014";
  return new Date(value).toLocaleString();
}

export function IssueDetailPage() {
  const { id, issueNumber: issueNumParam } = useParams<{ id: string; issueNumber: string }>();
  const projectId = Number(id);
  const issueNumber = Number(issueNumParam);
  const navigate = useNavigate();

  const { data: issue, isLoading } = useIssue(projectId, issueNumber);
  const { data: auditLog, isLoading: auditLoading } = useIssueAuditLog(projectId, issueNumber);
  const deleteIssue = useDeleteIssue();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);

  const handleDelete = () => {
    deleteIssue.mutate(
      { projectId, issueNumber },
      { onSuccess: () => navigate(`/projects/${projectId}`) },
    );
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="text-muted-foreground">Loading...</div>
      </div>
    );
  }

  if (!issue) {
    return (
      <div className="space-y-6">
        <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}`)}>
          <ArrowLeft className="size-4 mr-1" /> Back to project
        </Button>
        <div className="text-muted-foreground">Issue not found.</div>
      </div>
    );
  }

  const hasReproduction =
    issue.stepsToReproduce || issue.expectedBehavior || issue.actualBehavior ||
    issue.affectedComponent || issue.stackTrace;
  const hasResolution = issue.rootCause || issue.resolution;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}`)}>
            <ArrowLeft className="size-4" />
          </Button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold">{issue.name}</h1>
              <Badge variant="outline">{issue.issueId}</Badge>
              <span
                className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ${stateColors[issue.state] ?? ""}`}
              >
                {issue.state}
              </span>
            </div>
            <p className="text-sm text-muted-foreground mt-1">{issue.description}</p>
          </div>
        </div>
        <Button
          variant="outline"
          size="sm"
          className="text-destructive"
          onClick={() => setShowDeleteDialog(true)}
        >
          <Trash2 className="size-4 mr-1" /> Delete
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <Shield className="size-4" /> Definition
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <div className="text-xs text-muted-foreground mb-1">Type</div>
              <Badge variant="outline">{issue.issueType}</Badge>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Severity</div>
              <Badge variant={severityVariant[issue.severity] ?? "outline"}>
                {issue.severity}
              </Badge>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Priority</div>
              <Badge variant="outline">{issue.priority}</Badge>
            </div>
          </div>
        </CardContent>
      </Card>

      {hasReproduction && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <FileText className="size-4" /> Reproduction
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {issue.stepsToReproduce && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Steps to Reproduce</div>
                <p className="text-sm whitespace-pre-wrap">{issue.stepsToReproduce}</p>
              </div>
            )}
            {issue.expectedBehavior && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Expected Behavior</div>
                <p className="text-sm whitespace-pre-wrap">{issue.expectedBehavior}</p>
              </div>
            )}
            {issue.actualBehavior && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Actual Behavior</div>
                <p className="text-sm whitespace-pre-wrap">{issue.actualBehavior}</p>
              </div>
            )}
            {issue.affectedComponent && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Affected Component</div>
                <p className="text-sm font-mono">{issue.affectedComponent}</p>
              </div>
            )}
            {issue.stackTrace && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Stack Trace</div>
                <pre className="text-xs bg-muted p-3 rounded-md overflow-auto max-h-60">
                  {issue.stackTrace}
                </pre>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {hasResolution && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Resolution</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {issue.rootCause && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Root Cause</div>
                <p className="text-sm whitespace-pre-wrap">{issue.rootCause}</p>
              </div>
            )}
            {issue.resolution && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Resolution</div>
                <p className="text-sm whitespace-pre-wrap">{issue.resolution}</p>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {issue.linkedWorkPackages.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Package className="size-4" /> Related Work Packages
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>ID</TableHead>
                    <TableHead>Name</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Priority</TableHead>
                    <TableHead>State</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {issue.linkedWorkPackages.map((wp) => {
                    const wpNum = wp.workPackageId.split("-wp-")[1];
                    return (
                      <TableRow
                        key={wp.workPackageId}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => navigate(`/projects/${projectId}/work-packages/${wpNum}`)}
                      >
                        <TableCell className="font-mono text-sm">{wp.workPackageId}</TableCell>
                        <TableCell className="text-sm">{wp.name}</TableCell>
                        <TableCell><Badge variant="outline">{wp.type}</Badge></TableCell>
                        <TableCell><Badge variant="outline">{wp.priority}</Badge></TableCell>
                        <TableCell>
                          <span className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ${stateColors[wp.state] ?? ""}`}>
                            {wp.state}
                          </span>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      )}

      {issue.attachments.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Paperclip className="size-4" /> Attachments
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>File</TableHead>
                    <TableHead>Path</TableHead>
                    <TableHead>Description</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {issue.attachments.map((att, idx) => (
                    <TableRow key={idx}>
                      <TableCell className="font-mono text-sm">{att.fileName}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">{att.relativePath}</TableCell>
                      <TableCell className="text-sm">{att.description ?? "\u2014"}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <Clock className="size-4" /> Timeline
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-4 text-sm">
            <div>
              <div className="text-xs text-muted-foreground mb-1">Created</div>
              <div>{formatDate(issue.createdAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Started</div>
              <div>{formatDate(issue.startedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Completed</div>
              <div>{formatDate(issue.completedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Resolved</div>
              <div>{formatDate(issue.resolvedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Updated</div>
              <div>{formatDate(issue.updatedAt)}</div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Audit Log</CardTitle>
        </CardHeader>
        <CardContent>
          {auditLoading ? (
            <div className="text-muted-foreground text-sm">Loading audit log...</div>
          ) : !auditLog?.length ? (
            <div className="text-muted-foreground text-sm">No audit entries.</div>
          ) : (
            <div className="rounded-md border">
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
                  {auditLog.map((entry, idx) => (
                    <TableRow key={idx}>
                      <TableCell className="text-xs text-muted-foreground whitespace-nowrap">
                        {new Date(entry.changedAt).toLocaleString()}
                      </TableCell>
                      <TableCell className="font-mono text-xs">{entry.fieldName}</TableCell>
                      <TableCell className="text-xs max-w-[200px] truncate">
                        {entry.oldValue ?? "\u2014"}
                      </TableCell>
                      <TableCell className="text-xs max-w-[200px] truncate">
                        {entry.newValue ?? "\u2014"}
                      </TableCell>
                      <TableCell className="text-xs">{entry.changedBy}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete issue?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete <strong>{issue.name}</strong> ({issue.issueId}).
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
