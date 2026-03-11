import { useState } from "react";
import { useParams, useNavigate } from "react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ArrowLeft, Trash2, Lightbulb, Package, Paperclip, Clock, Pencil, X, Save } from "lucide-react";
import { useFeatureRequest, useDeleteFeatureRequest, useUpdateFeatureRequest, useManageUserStories } from "@/hooks/use-feature-requests";
import { updateFeatureRequestSchema, type UpdateFeatureRequestInput, featureCategories, priorities, featureStatuses } from "@/lib/schemas";
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
import { UserStoryCard } from "@/components/user-story-card";
import { AddUserStoryForm } from "@/components/add-user-story-form";
import { MarkdownContent } from "@/components/markdown-content";
import type { FeatureRequest, UserStory } from "@/types";

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

function formatDate(value: string | null): string {
  if (!value) return "\u2014";
  return new Date(value).toLocaleString();
}

function frToFormValues(fr: FeatureRequest): UpdateFeatureRequestInput {
  return {
    name: fr.name,
    description: fr.description,
    category: fr.category as UpdateFeatureRequestInput["category"],
    priority: fr.priority as UpdateFeatureRequestInput["priority"],
    businessValue: fr.businessValue ?? "",
    requester: fr.requester ?? "",
    acceptanceSummary: fr.acceptanceSummary ?? "",
  };
}

function computeDiff(original: UpdateFeatureRequestInput, current: UpdateFeatureRequestInput): Record<string, unknown> {
  const diff: Record<string, unknown> = {};
  for (const key of Object.keys(current) as (keyof UpdateFeatureRequestInput)[]) {
    const curr = current[key];
    const orig = original[key];
    if (curr !== orig && curr !== undefined) {
      if (curr === "" && (orig === "" || orig === undefined)) continue;
      diff[key] = curr === "" ? null : curr;
    }
  }
  return diff;
}

export function FeatureRequestDetailPage() {
  const { id, featureNumber: frNumParam } = useParams<{ id: string; featureNumber: string }>();
  const projectId = Number(id);
  const frNumber = Number(frNumParam);
  const navigate = useNavigate();

  const { data: fr, isLoading } = useFeatureRequest(projectId, frNumber);
  const deleteFr = useDeleteFeatureRequest();
  const updateFr = useUpdateFeatureRequest();
  const manageStories = useManageUserStories();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [statusToChange, setStatusToChange] = useState<string | null>(null);

  const form = useForm<UpdateFeatureRequestInput>({
    resolver: zodResolver(updateFeatureRequestSchema),
  });

  const handleEdit = () => {
    if (!fr) return;
    form.reset(frToFormValues(fr));
    setIsEditing(true);
  };

  const handleCancel = () => {
    form.reset();
    setIsEditing(false);
  };

  const handleSave = () => {
    if (!fr) return;
    const current = form.getValues();
    const original = frToFormValues(fr);
    const diff = computeDiff(original, current);

    if (Object.keys(diff).length === 0) {
      setIsEditing(false);
      return;
    }

    updateFr.mutate(
      { projectId, frNumber, data: diff },
      {
        onSuccess: () => {
          toast.success("Feature request updated");
          setIsEditing(false);
        },
        onError: (error) => {
          toast.error(`Failed to update: ${error.message}`);
        },
      },
    );
  };

  const handleDelete = () => {
    deleteFr.mutate(
      { projectId, frNumber },
      { onSuccess: () => navigate(`/projects/${projectId}/feature-requests`) },
    );
  };

  const handleStatusChange = () => {
    if (!statusToChange) return;
    updateFr.mutate(
      { projectId, frNumber, data: { status: statusToChange } },
      {
        onSuccess: () => {
          toast.success(`Status changed to ${statusToChange}`);
          setStatusToChange(null);
        },
        onError: (error) => {
          toast.error(`Failed to change status: ${error.message}`);
          setStatusToChange(null);
        },
      },
    );
  };

  if (isLoading) {
    return <div className="text-muted-foreground">Loading...</div>;
  }

  if (!fr) {
    return (
      <div className="space-y-6">
        <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}/feature-requests`)}>
          <ArrowLeft className="size-4 mr-1" /> Back
        </Button>
        <div className="text-muted-foreground">Feature request not found.</div>
      </div>
    );
  }

  const handleAddStory = (story: UserStory) => {
    manageStories.mutate(
      { projectId, frNumber, data: { action: "Add", ...story } },
      {
        onSuccess: () => toast.success("User story added"),
        onError: (error) => toast.error(`Failed to add user story: ${error.message}`),
      },
    );
  };

  const handleUpdateStory = (index: number, story: UserStory) => {
    manageStories.mutate(
      { projectId, frNumber, data: { action: "Update", index, ...story } },
      {
        onSuccess: () => toast.success("User story updated"),
        onError: (error) => toast.error(`Failed to update user story: ${error.message}`),
      },
    );
  };

  const handleRemoveStory = (index: number) => {
    manageStories.mutate(
      { projectId, frNumber, data: { action: "Remove", index } },
      {
        onSuccess: () => toast.success("User story removed"),
        onError: (error) => toast.error(`Failed to remove user story: ${error.message}`),
      },
    );
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}/feature-requests`)}>
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
                <h1 className="text-2xl font-bold">{fr.name}</h1>
              )}
              <Badge variant="outline">{fr.featureRequestId}</Badge>
              {/* Status quick-action */}
              <Select
                value={fr.status}
                onValueChange={(v) => {
                  if (v !== fr.status) setStatusToChange(v);
                }}
              >
                <SelectTrigger className={`w-auto h-auto py-0.5 px-2 text-xs border-0 ${stateColorClass(fr.status, "feature")}`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {featureStatuses.map((s) => (
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
              <Button size="sm" onClick={handleSave} disabled={updateFr.isPending}>
                <Save className="size-4 mr-1" /> {updateFr.isPending ? "Saving..." : "Save"}
              </Button>
              <Button variant="outline" size="sm" onClick={handleCancel}>
                <X className="size-4 mr-1" /> Cancel
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" size="sm" onClick={handleEdit}>
                <Pencil className="size-4 mr-1" /> Edit
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="text-destructive"
                onClick={() => setShowDeleteDialog(true)}
              >
                <Trash2 className="size-4 mr-1" /> Delete
              </Button>
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
            <MarkdownContent content={fr.description} />
          )}
        </CardContent>
      </Card>

      {/* Definition Card */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2.5">
            <Lightbulb className="size-4" /> Definition
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-3 gap-6">
            <div>
              <div className="text-sm text-muted-foreground mb-1">Category</div>
              {isEditing ? (
                <Select onValueChange={(v) => form.setValue("category", v as UpdateFeatureRequestInput["category"])} value={form.watch("category") ?? fr.category}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {featureCategories.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : (
                <Badge variant={categoryVariant[fr.category] ?? "outline"}>{fr.category}</Badge>
              )}
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Priority</div>
              {isEditing ? (
                <Select onValueChange={(v) => form.setValue("priority", v as UpdateFeatureRequestInput["priority"])} value={form.watch("priority") ?? fr.priority}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {priorities.map((p) => <SelectItem key={p} value={p}>{p}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : (
                <Badge variant={priorityVariant[fr.priority] ?? "outline"}>{fr.priority}</Badge>
              )}
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Requester</div>
              {isEditing ? (
                <Input {...form.register("requester")} />
              ) : (
                <div className="text-sm">{fr.requester || "\u2014"}</div>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* User Stories */}
      {(fr.userStories.length > 0 || !isEditing) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">User Stories</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {fr.userStories.length === 0 && !isEditing ? (
              <p className="text-sm text-muted-foreground">No user stories yet.</p>
            ) : (
              fr.userStories.map((story, idx) => (
                <UserStoryCard
                  key={idx}
                  story={story}
                  index={idx}
                  onUpdate={handleUpdateStory}
                  onRemove={handleRemoveStory}
                  disabled={manageStories.isPending}
                />
              ))
            )}
            <AddUserStoryForm onAdd={handleAddStory} disabled={manageStories.isPending} />
          </CardContent>
        </Card>
      )}

      {/* Business Value */}
      {(fr.businessValue || isEditing) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Business Value</CardTitle>
          </CardHeader>
          <CardContent>
            {isEditing ? (
              <Textarea rows={2} {...form.register("businessValue")} />
            ) : (
              <MarkdownContent content={fr.businessValue} />
            )}
          </CardContent>
        </Card>
      )}

      {/* Acceptance Summary */}
      {(fr.acceptanceSummary || isEditing) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Acceptance Summary</CardTitle>
          </CardHeader>
          <CardContent>
            {isEditing ? (
              <Textarea rows={3} {...form.register("acceptanceSummary")} />
            ) : (
              <MarkdownContent content={fr.acceptanceSummary} />
            )}
          </CardContent>
        </Card>
      )}

      {/* Related Work Packages */}
      {fr.linkedWorkPackages.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2.5">
              <Package className="size-4" /> Related Work Packages
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="rounded-lg border">
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
                          <span className={stateColorClass(wp.state)}>{wp.state}</span>
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
            <CardTitle className="text-base flex items-center gap-2.5">
              <Paperclip className="size-4" /> Attachments
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="rounded-lg border">
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

      {/* Timeline */}
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
              <div>{formatDate(fr.createdAt)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Started</div>
              <div>{formatDate(fr.startedAt)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Completed</div>
              <div>{formatDate(fr.completedAt)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Resolved</div>
              <div>{formatDate(fr.resolvedAt)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Updated</div>
              <div>{formatDate(fr.updatedAt)}</div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Status change confirmation */}
      <AlertDialog open={!!statusToChange} onOpenChange={(open) => !open && setStatusToChange(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Change feature request status?</AlertDialogTitle>
            <AlertDialogDescription>
              Transition from <strong>{fr.status}</strong> to <strong>{statusToChange}</strong>.
              State-driven timestamps will be updated automatically.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleStatusChange}>Confirm</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete confirmation */}
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
