import { useState } from "react";
import { useParams, useNavigate } from "react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ArrowLeft, Trash2, FileText, Shield, Pencil, X, Save } from "lucide-react";
import { useIssue, useIssueAuditLog, useDeleteIssue, useUpdateIssue } from "@/hooks/use-issues";
import { usePermissions } from "@/hooks/use-permissions";
import { computeDiff } from "@/lib/form-utils";
import { TimelineSection } from "@/components/timeline-section";
import { AttachmentsTable } from "@/components/attachments-table";
import { RelatedWorkPackages } from "@/components/related-work-packages";
import { AuditLogSection } from "@/components/audit-log-section";
import { StateChangeConfirmDialog, DeleteConfirmDialog } from "@/components/confirm-dialogs";
import { PageTransition } from "@/components/page-transition";
import { DetailSkeleton } from "@/components/loading-skeletons";
import { updateIssueSchema, type UpdateIssueInput, issueTypes, issueSeverities, priorities, completionStates } from "@/lib/schemas";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { stateColorClass } from "@/lib/state-colors";
import { MarkdownContent } from "@/components/markdown-content";
import type { Issue } from "@/types";

const severityVariant: Record<string, "destructive" | "default" | "secondary" | "outline"> = {
  Critical: "destructive",
  Major: "default",
  Minor: "secondary",
  Trivial: "outline",
};

function issueToFormValues(issue: Issue): UpdateIssueInput {
  return {
    name: issue.name,
    description: issue.description,
    issueType: issue.issueType as UpdateIssueInput["issueType"],
    severity: issue.severity as UpdateIssueInput["severity"],
    priority: issue.priority as UpdateIssueInput["priority"],
    stepsToReproduce: issue.stepsToReproduce ?? "",
    expectedBehavior: issue.expectedBehavior ?? "",
    actualBehavior: issue.actualBehavior ?? "",
    affectedComponent: issue.affectedComponent ?? "",
    stackTrace: issue.stackTrace ?? "",
    rootCause: issue.rootCause ?? "",
    resolution: issue.resolution ?? "",
  };
}


export function IssueDetailPage() {
  const { id, issueNumber: issueNumParam } = useParams<{ id: string; issueNumber: string }>();
  const projectId = Number(id);
  const issueNumber = Number(issueNumParam);
  const navigate = useNavigate();

  const { data: issue, isLoading } = useIssue(projectId, issueNumber);
  const { data: auditLog, isLoading: auditLoading } = useIssueAuditLog(projectId, issueNumber);
  const { canEdit, canDelete } = usePermissions(projectId);
  const deleteIssue = useDeleteIssue();
  const updateIssue = useUpdateIssue();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [auditExpanded, setAuditExpanded] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [stateToChange, setStateToChange] = useState<string | null>(null);

  const form = useForm<UpdateIssueInput>({
    resolver: zodResolver(updateIssueSchema),
  });

  const handleEdit = () => {
    if (!issue) return;
    form.reset(issueToFormValues(issue));
    setIsEditing(true);
  };

  const handleCancel = () => {
    form.reset();
    setIsEditing(false);
  };

  const handleSave = () => {
    if (!issue) return;
    const current = form.getValues();
    const original = issueToFormValues(issue);
    const diff = computeDiff(original, current);

    if (Object.keys(diff).length === 0) {
      setIsEditing(false);
      return;
    }

    updateIssue.mutate(
      { projectId, issueNumber, data: diff },
      {
        onSuccess: () => {
          toast.success("Issue updated");
          setIsEditing(false);
        },
        onError: (error) => {
          toast.error(`Failed to update: ${error.message}`);
        },
      },
    );
  };

  const handleDelete = () => {
    deleteIssue.mutate(
      { projectId, issueNumber },
      { onSuccess: () => navigate(`/projects/${projectId}/issues`) },
    );
  };

  const handleStateChange = () => {
    if (!stateToChange) return;
    updateIssue.mutate(
      { projectId, issueNumber, data: { state: stateToChange } },
      {
        onSuccess: () => {
          toast.success(`State changed to ${stateToChange}`);
          setStateToChange(null);
        },
        onError: (error) => {
          toast.error(`Failed to change state: ${error.message}`);
          setStateToChange(null);
        },
      },
    );
  };

  if (isLoading) {
    return <DetailSkeleton />;
  }

  if (!issue) {
    return (
      <div className="space-y-6">
        <Button variant="ghost" size="sm" onClick={() => navigate(-1)}>
          <ArrowLeft className="size-4 mr-1" /> Back
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
    <PageTransition>
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={() => navigate(-1)}>
            <ArrowLeft className="size-4" />
          </Button>
          <div>
            <div className="flex items-center gap-2 flex-wrap">
              {isEditing ? (
                <Input
                  className="text-2xl font-bold h-auto py-0 px-1 max-w-md"
                  {...form.register("name")}
                />
              ) : (
                <h1 className="text-2xl font-bold">{issue.name}</h1>
              )}
              <Badge variant="outline">{issue.issueId}</Badge>
              {/* State quick-action */}
              <Select
                value={issue.state}
                onValueChange={(v) => {
                  if (v !== issue.state) setStateToChange(v);
                }}
              >
                <SelectTrigger className={`w-auto h-auto py-0.5 px-2 text-xs border-0 ${stateColorClass(issue.state)}`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {completionStates.map((s) => (
                    <SelectItem key={s} value={s}>{s}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {isEditing ? (
            <>
              <Button size="sm" onClick={handleSave} disabled={updateIssue.isPending}>
                <Save className="size-4 mr-1" /> {updateIssue.isPending ? "Saving..." : "Save"}
              </Button>
              <Button variant="outline" size="sm" onClick={handleCancel}>
                <X className="size-4 mr-1" /> Cancel
              </Button>
            </>
          ) : (
            <>
              {canEdit && (
                <Button variant="outline" size="sm" onClick={handleEdit}>
                  <Pencil className="size-4 mr-1" /> Edit
                </Button>
              )}
              {canDelete && (
                <Button
                  variant="outline"
                  size="sm"
                  className="text-destructive"
                  onClick={() => setShowDeleteDialog(true)}
                >
                  <Trash2 className="size-4 mr-1" /> Delete
                </Button>
              )}
            </>
          )}
        </div>
      </div>

      {/* Description Card */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Description</CardTitle>
        </CardHeader>
        <CardContent>
          {isEditing ? (
            <Textarea rows={4} {...form.register("description")} />
          ) : (
            <MarkdownContent content={issue.description} />
          )}
        </CardContent>
      </Card>

      {/* Definition Card */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2.5">
            <Shield className="size-4" /> Definition
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-3 gap-6">
            <div>
              <div className="text-sm text-muted-foreground mb-1">Type</div>
              {isEditing ? (
                <Select onValueChange={(v) => form.setValue("issueType", v as UpdateIssueInput["issueType"])} value={form.watch("issueType") ?? issue.issueType}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {issueTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : (
                <Badge variant="outline">{issue.issueType}</Badge>
              )}
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Severity</div>
              {isEditing ? (
                <Select onValueChange={(v) => form.setValue("severity", v as UpdateIssueInput["severity"])} value={form.watch("severity") ?? issue.severity}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {issueSeverities.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : (
                <Badge variant={severityVariant[issue.severity] ?? "outline"}>{issue.severity}</Badge>
              )}
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Priority</div>
              {isEditing ? (
                <Select onValueChange={(v) => form.setValue("priority", v as UpdateIssueInput["priority"])} value={form.watch("priority") ?? issue.priority}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {priorities.map((p) => <SelectItem key={p} value={p}>{p}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : (
                <Badge variant="outline">{issue.priority}</Badge>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Reproduction Card */}
      {(hasReproduction || isEditing) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2.5">
              <FileText className="size-4" /> Reproduction
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {isEditing ? (
              <>
                <div>
                  <div className="text-sm font-medium text-muted-foreground mb-1">Steps to Reproduce</div>
                  <Textarea rows={3} {...form.register("stepsToReproduce")} />
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Expected Behavior</div>
                    <Textarea rows={2} {...form.register("expectedBehavior")} />
                  </div>
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Actual Behavior</div>
                    <Textarea rows={2} {...form.register("actualBehavior")} />
                  </div>
                </div>
                <div>
                  <div className="text-sm font-medium text-muted-foreground mb-1">Affected Component</div>
                  <Input {...form.register("affectedComponent")} />
                </div>
                <div>
                  <div className="text-sm font-medium text-muted-foreground mb-1">Stack Trace</div>
                  <Textarea rows={3} className="font-mono text-xs" {...form.register("stackTrace")} />
                </div>
              </>
            ) : (
              <>
                {issue.stepsToReproduce && (
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Steps to Reproduce</div>
                    <MarkdownContent content={issue.stepsToReproduce} />
                  </div>
                )}
                {issue.expectedBehavior && (
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Expected Behavior</div>
                    <MarkdownContent content={issue.expectedBehavior} />
                  </div>
                )}
                {issue.actualBehavior && (
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Actual Behavior</div>
                    <MarkdownContent content={issue.actualBehavior} />
                  </div>
                )}
                {issue.affectedComponent && (
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Affected Component</div>
                    <p className="text-sm font-mono">{issue.affectedComponent}</p>
                  </div>
                )}
                {issue.stackTrace && (
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Stack Trace</div>
                    <pre className="text-xs bg-muted p-3 rounded-md overflow-auto max-h-60">{issue.stackTrace}</pre>
                  </div>
                )}
              </>
            )}
          </CardContent>
        </Card>
      )}

      {/* Resolution Card */}
      {(hasResolution || isEditing) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Resolution</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {isEditing ? (
              <>
                <div>
                  <div className="text-sm font-medium text-muted-foreground mb-1">Root Cause</div>
                  <Textarea rows={2} {...form.register("rootCause")} />
                </div>
                <div>
                  <div className="text-sm font-medium text-muted-foreground mb-1">Resolution</div>
                  <Textarea rows={2} {...form.register("resolution")} />
                </div>
              </>
            ) : (
              <>
                {issue.rootCause && (
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Root Cause</div>
                    <MarkdownContent content={issue.rootCause} />
                  </div>
                )}
                {issue.resolution && (
                  <div>
                    <div className="text-sm font-medium text-muted-foreground mb-1">Resolution</div>
                    <MarkdownContent content={issue.resolution} />
                  </div>
                )}
              </>
            )}
          </CardContent>
        </Card>
      )}

      <RelatedWorkPackages items={issue.linkedWorkPackages} projectId={projectId} />

      <AttachmentsTable attachments={issue.attachments} />

      <TimelineSection
        createdAt={issue.createdAt}
        startedAt={issue.startedAt}
        completedAt={issue.completedAt}
        resolvedAt={issue.resolvedAt}
        updatedAt={issue.updatedAt}
      />

      <AuditLogSection
        entries={auditLog}
        isLoading={auditLoading}
        expanded={auditExpanded}
        onExpandedChange={setAuditExpanded}
      />

      <StateChangeConfirmDialog
        open={!!stateToChange}
        onOpenChange={(open) => !open && setStateToChange(null)}
        entityType="issue"
        currentState={issue.state}
        newState={stateToChange}
        onConfirm={handleStateChange}
      />

      <DeleteConfirmDialog
        open={showDeleteDialog}
        onOpenChange={setShowDeleteDialog}
        entityType="issue"
        entityName={issue.name}
        entityId={issue.issueId}
        onConfirm={handleDelete}
      />
    </div>
    </PageTransition>
  );
}
