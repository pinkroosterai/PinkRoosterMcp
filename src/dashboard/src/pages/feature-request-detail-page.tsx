import { useState } from "react";
import { useParams, useNavigate } from "react-router";
import { ArrowLeft, Trash2, Lightbulb, Package, Paperclip, Clock } from "lucide-react";
import { useFeatureRequest, useDeleteFeatureRequest } from "@/hooks/use-feature-requests";
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

const featureStatusColors: Record<string, string> = {
  Proposed: "bg-gray-100 text-gray-700",
  UnderReview: "bg-blue-100 text-blue-700",
  Approved: "bg-indigo-100 text-indigo-700",
  Scheduled: "bg-purple-100 text-purple-700",
  InProgress: "bg-yellow-100 text-yellow-700",
  Completed: "bg-green-100 text-green-700",
  Rejected: "bg-red-100 text-red-700",
  Deferred: "bg-orange-100 text-orange-700",
};

const categoryVariant: Record<string, "default" | "secondary" | "outline"> = {
  Feature: "default",
  Enhancement: "secondary",
  Improvement: "outline",
};

const priorityVariant: Record<string, "destructive" | "default" | "secondary" | "outline"> = {
  Critical: "destructive",
  High: "default",
  Medium: "secondary",
  Low: "outline",
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

export function FeatureRequestDetailPage() {
  const { id, featureNumber: frNumParam } = useParams<{ id: string; featureNumber: string }>();
  const projectId = Number(id);
  const frNumber = Number(frNumParam);
  const navigate = useNavigate();

  const { data: fr, isLoading } = useFeatureRequest(projectId, frNumber);
  const deleteFr = useDeleteFeatureRequest();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);

  const handleDelete = () => {
    deleteFr.mutate(
      { projectId, frNumber },
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

  if (!fr) {
    return (
      <div className="space-y-6">
        <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}`)}>
          <ArrowLeft className="size-4 mr-1" /> Back to project
        </Button>
        <div className="text-muted-foreground">Feature request not found.</div>
      </div>
    );
  }

  const hasUserStoryOrBV = fr.userStory || fr.businessValue;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}`)}>
            <ArrowLeft className="size-4" />
          </Button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold">{fr.name}</h1>
              <Badge variant="outline">{fr.featureRequestId}</Badge>
              <span
                className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ${featureStatusColors[fr.status] ?? ""}`}
              >
                {fr.status}
              </span>
            </div>
            <p className="text-sm text-muted-foreground mt-1">{fr.description}</p>
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

      {/* Definition Card */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <Lightbulb className="size-4" /> Definition
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <div className="text-xs text-muted-foreground mb-1">Category</div>
              <Badge variant={categoryVariant[fr.category] ?? "outline"}>
                {fr.category}
              </Badge>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Priority</div>
              <Badge variant={priorityVariant[fr.priority] ?? "outline"}>
                {fr.priority}
              </Badge>
            </div>
            {fr.requester && (
              <div>
                <div className="text-xs text-muted-foreground mb-1">Requester</div>
                <div className="text-sm">{fr.requester}</div>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* User Story & Business Value */}
      {hasUserStoryOrBV && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">User Story & Business Value</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {fr.userStory && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">User Story</div>
                <p className="text-sm whitespace-pre-wrap">{fr.userStory}</p>
              </div>
            )}
            {fr.businessValue && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Business Value</div>
                <p className="text-sm whitespace-pre-wrap">{fr.businessValue}</p>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Acceptance Summary */}
      {fr.acceptanceSummary && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Acceptance Summary</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm whitespace-pre-wrap">{fr.acceptanceSummary}</p>
          </CardContent>
        </Card>
      )}

      {/* Related Work Packages */}
      {fr.linkedWorkPackages.length > 0 && (
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
                  {fr.linkedWorkPackages.map((wp) => {
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

      {/* Attachments */}
      {fr.attachments.length > 0 && (
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
                  {fr.attachments.map((att, idx) => (
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

      {/* Timeline Card */}
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
              <div>{formatDate(fr.createdAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Started</div>
              <div>{formatDate(fr.startedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Completed</div>
              <div>{formatDate(fr.completedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Resolved</div>
              <div>{formatDate(fr.resolvedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Updated</div>
              <div>{formatDate(fr.updatedAt)}</div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Delete Dialog */}
      <AlertDialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete feature request?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete <strong>{fr.name}</strong> ({fr.featureRequestId}).
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
