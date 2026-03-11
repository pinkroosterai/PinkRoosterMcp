import { useState } from "react";
import { useParams, useNavigate, Link } from "react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { ArrowLeft, Trash2, Layers, ChevronDown, ChevronRight, CheckCircle2, Circle, Clock, Pencil, X, Save } from "lucide-react";
import { useWorkPackage, useDeleteWorkPackage, useDeletePhase, useDeleteTask, useUpdateWorkPackage, useUpdateTask } from "@/hooks/use-work-packages";
import { updateWorkPackageSchema, type UpdateWorkPackageInput, workPackageTypes, priorities, completionStates } from "@/lib/schemas";
import type { TaskDep, Phase as PhaseType, WpTask, WorkPackage, StateChangeDto } from "@/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { stateColorClass } from "@/lib/state-colors";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

const typeVariant: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
  Feature: "default",
  BugFix: "destructive",
  Refactor: "secondary",
  Spike: "outline",
  Chore: "outline",
};

const priorityVariant: Record<string, "destructive" | "default" | "secondary" | "outline"> = {
  Critical: "destructive",
  High: "default",
  Medium: "secondary",
  Low: "outline",
};

function formatDate(value: string | null | undefined): string {
  if (!value) return "\u2014";
  return new Date(value).toLocaleString();
}

function showCascadeToasts(stateChanges?: StateChangeDto[] | null) {
  if (!stateChanges?.length) return;
  for (const sc of stateChanges) {
    toast.info(`${sc.entityId}: ${sc.oldState} \u2192 ${sc.newState} \u2014 ${sc.reason}`);
  }
}

function wpToFormValues(wp: WorkPackage): UpdateWorkPackageInput {
  return {
    name: wp.name,
    description: wp.description,
    type: wp.type as UpdateWorkPackageInput["type"],
    priority: wp.priority as UpdateWorkPackageInput["priority"],
    plan: wp.plan ?? "",
    estimatedComplexity: wp.estimatedComplexity ?? undefined,
    estimationRationale: wp.estimationRationale ?? "",
  };
}

function computeDiff(original: UpdateWorkPackageInput, current: UpdateWorkPackageInput): Record<string, unknown> {
  const diff: Record<string, unknown> = {};
  for (const key of Object.keys(current) as (keyof UpdateWorkPackageInput)[]) {
    const curr = current[key];
    const orig = original[key];
    if (curr !== orig && curr !== undefined) {
      if (curr === "" && (orig === "" || orig === undefined)) continue;
      diff[key] = curr === "" ? null : curr;
    }
  }
  return diff;
}

export function WorkPackageDetailPage() {
  const { id, wpNumber: wpNumParam } = useParams<{ id: string; wpNumber: string }>();
  const projectId = Number(id);
  const wpNumber = Number(wpNumParam);
  const navigate = useNavigate();

  const { data: wp, isLoading } = useWorkPackage(projectId, wpNumber);
  const deleteWorkPackage = useDeleteWorkPackage();
  const deletePhase = useDeletePhase();
  const deleteTask = useDeleteTask();
  const updateWp = useUpdateWorkPackage();
  const updateTaskMutation = useUpdateTask();

  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [phaseToDelete, setPhaseToDelete] = useState<PhaseType | null>(null);
  const [taskToDelete, setTaskToDelete] = useState<WpTask | null>(null);
  const [expandedPhases, setExpandedPhases] = useState<Set<number>>(new Set());
  const [expandedTasks, setExpandedTasks] = useState<Set<string>>(new Set());
  const [isEditing, setIsEditing] = useState(false);
  const [stateToChange, setStateToChange] = useState<string | null>(null);

  const form = useForm<UpdateWorkPackageInput>({
    resolver: zodResolver(updateWorkPackageSchema),
  });

  const handleEdit = () => {
    if (!wp) return;
    form.reset(wpToFormValues(wp));
    setIsEditing(true);
  };

  const handleCancel = () => {
    form.reset();
    setIsEditing(false);
  };

  const handleSave = () => {
    if (!wp) return;
    const current = form.getValues();
    const original = wpToFormValues(wp);
    const diff = computeDiff(original, current);

    if (Object.keys(diff).length === 0) {
      setIsEditing(false);
      return;
    }

    updateWp.mutate(
      { projectId, wpNumber, data: diff },
      {
        onSuccess: (data) => {
          toast.success("Work package updated");
          showCascadeToasts(data.stateChanges);
          setIsEditing(false);
        },
        onError: (error) => {
          toast.error(`Failed to update: ${error.message}`);
        },
      },
    );
  };

  const handleDelete = () => {
    deleteWorkPackage.mutate(
      { projectId, wpNumber },
      { onSuccess: () => navigate(`/projects/${projectId}/work-packages`) },
    );
  };

  const handlePhaseDelete = () => {
    if (!phaseToDelete) return;
    deletePhase.mutate(
      { projectId, wpNumber, phaseNumber: phaseToDelete.phaseNumber },
      { onSettled: () => setPhaseToDelete(null) },
    );
  };

  const handleTaskDelete = () => {
    if (!taskToDelete) return;
    deleteTask.mutate(
      { projectId, wpNumber, taskNumber: taskToDelete.taskNumber },
      { onSettled: () => setTaskToDelete(null) },
    );
  };

  const handleStateChange = () => {
    if (!stateToChange) return;
    updateWp.mutate(
      { projectId, wpNumber, data: { state: stateToChange } },
      {
        onSuccess: (data) => {
          toast.success(`State changed to ${stateToChange}`);
          showCascadeToasts(data.stateChanges);
          setStateToChange(null);
        },
        onError: (error) => {
          toast.error(`Failed to change state: ${error.message}`);
          setStateToChange(null);
        },
      },
    );
  };

  const handleTaskStateChange = (task: WpTask, newState: string) => {
    updateTaskMutation.mutate(
      { projectId, wpNumber, taskNumber: task.taskNumber, data: { state: newState } },
      {
        onSuccess: (data) => {
          toast.success(`Task state changed to ${newState}`);
          showCascadeToasts(data.stateChanges);
        },
        onError: (error) => {
          toast.error(`Failed to change task state: ${error.message}`);
        },
      },
    );
  };

  const togglePhase = (phaseId: number) => {
    setExpandedPhases((prev) => {
      const next = new Set(prev);
      if (next.has(phaseId)) {
        next.delete(phaseId);
      } else {
        next.add(phaseId);
      }
      return next;
    });
  };

  const toggleTask = (taskKey: string) => {
    setExpandedTasks((prev) => {
      const next = new Set(prev);
      if (next.has(taskKey)) {
        next.delete(taskKey);
      } else {
        next.add(taskKey);
      }
      return next;
    });
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="text-muted-foreground">Loading...</div>
      </div>
    );
  }

  if (!wp) {
    return (
      <div className="space-y-6">
        <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}/work-packages`)}>
          <ArrowLeft className="size-4 mr-1" /> Back to project
        </Button>
        <div className="text-muted-foreground">Work package not found.</div>
      </div>
    );
  }

  const hasEstimation = wp.estimatedComplexity != null || wp.estimationRationale;
  const hasDependencies =
    (wp.blockedBy && wp.blockedBy.length > 0) || (wp.blocking && wp.blocking.length > 0);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}/work-packages`)}>
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
                <h1 className="text-2xl font-bold">{wp.name}</h1>
              )}
              <Badge variant="outline">{wp.workPackageId}</Badge>
              {isEditing ? (
                <Select
                  onValueChange={(v) => form.setValue("type", v as UpdateWorkPackageInput["type"])}
                  value={form.watch("type") ?? wp.type}
                >
                  <SelectTrigger className="w-auto h-auto py-0.5 px-2 text-xs">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {workPackageTypes.map((t) => (
                      <SelectItem key={t} value={t}>{t}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Badge variant={typeVariant[wp.type] ?? "outline"}>{wp.type}</Badge>
              )}
              {isEditing ? (
                <Select
                  onValueChange={(v) => form.setValue("priority", v as UpdateWorkPackageInput["priority"])}
                  value={form.watch("priority") ?? wp.priority}
                >
                  <SelectTrigger className="w-auto h-auto py-0.5 px-2 text-xs">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {priorities.map((p) => (
                      <SelectItem key={p} value={p}>{p}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Badge variant={priorityVariant[wp.priority] ?? "outline"}>{wp.priority}</Badge>
              )}
              {/* State quick-action */}
              <Select
                value={wp.state}
                onValueChange={(v) => {
                  if (v !== wp.state) setStateToChange(v);
                }}
              >
                <SelectTrigger className={`w-auto h-auto py-0.5 px-2 text-xs border-0 ${stateColorClass(wp.state)}`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {completionStates.map((s) => (
                    <SelectItem key={s} value={s}>{s}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {wp.state === "Blocked" && wp.previousActiveState && (
                <span className="text-xs text-muted-foreground">(was: {wp.previousActiveState})</span>
              )}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {isEditing ? (
            <>
              <Button size="sm" onClick={handleSave} disabled={updateWp.isPending}>
                <Save className="size-4 mr-1" /> {updateWp.isPending ? "Saving..." : "Save"}
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

      {/* Definition Card */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2.5">
            <Layers className="size-4" /> Definition
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <div className="text-sm font-medium text-muted-foreground mb-1">Description</div>
            {isEditing ? (
              <Textarea rows={3} {...form.register("description")} />
            ) : (
              <p className="text-sm whitespace-pre-wrap">{wp.description}</p>
            )}
          </div>
          {(wp.plan || isEditing) && (
            <div>
              <div className="text-sm font-medium text-muted-foreground mb-1">Plan</div>
              {isEditing ? (
                <Textarea rows={4} {...form.register("plan")} />
              ) : (
                <pre className="whitespace-pre-wrap text-sm bg-muted p-3 rounded-md">{wp.plan}</pre>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Estimation Card */}
      {(hasEstimation || isEditing) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Estimation</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground mb-1">
                Complexity (1-10)
              </div>
              {isEditing ? (
                <Input
                  type="number"
                  min={1}
                  max={10}
                  className="w-24"
                  {...form.register("estimatedComplexity")}
                />
              ) : wp.estimatedComplexity != null ? (
                <div className="text-sm font-medium">{wp.estimatedComplexity}</div>
              ) : (
                <div className="text-sm text-muted-foreground">{"\u2014"}</div>
              )}
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground mb-1">Rationale</div>
              {isEditing ? (
                <Textarea rows={2} {...form.register("estimationRationale")} />
              ) : wp.estimationRationale ? (
                <p className="text-sm whitespace-pre-wrap">{wp.estimationRationale}</p>
              ) : (
                <div className="text-sm text-muted-foreground">{"\u2014"}</div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Dependencies Card */}
      {hasDependencies && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Dependencies</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {wp.blockedBy && wp.blockedBy.length > 0 && (
              <div>
                <div className="text-sm font-medium text-muted-foreground mb-2">Blocked By</div>
                <div className="space-y-2">
                  {wp.blockedBy.map((dep, idx) => (
                    <div key={idx} className="flex items-center gap-2 text-sm">
                      <span className="font-medium">{dep.name}</span>
                      <span
                        className={stateColorClass(dep.state)}
                      >
                        {dep.state}
                      </span>
                      {dep.reason && (
                        <span className="text-muted-foreground">- {dep.reason}</span>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}
            {wp.blocking && wp.blocking.length > 0 && (
              <div>
                <div className="text-sm font-medium text-muted-foreground mb-2">Blocking</div>
                <div className="space-y-2">
                  {wp.blocking.map((dep, idx) => (
                    <div key={idx} className="flex items-center gap-2 text-sm">
                      <span className="font-medium">{dep.name}</span>
                      <span
                        className={stateColorClass(dep.state)}
                      >
                        {dep.state}
                      </span>
                      {dep.reason && (
                        <span className="text-muted-foreground">- {dep.reason}</span>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Linked Issue Card */}
      {wp.linkedIssueId && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Linked Issue</CardTitle>
          </CardHeader>
          <CardContent>
            <Link to={`/projects/${id}/issues/${wp.linkedIssueId.split("-issue-")[1]}`}>
              <Badge variant="outline" className="cursor-pointer hover:bg-muted">
                {wp.linkedIssueId}
              </Badge>
            </Link>
          </CardContent>
        </Card>
      )}

      {/* Linked Feature Request Card */}
      {wp.linkedFeatureRequestId && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Linked Feature Request</CardTitle>
          </CardHeader>
          <CardContent>
            <Link to={`/projects/${id}/feature-requests/${wp.linkedFeatureRequestId.split("-fr-")[1]}`}>
              <Badge variant="outline" className="cursor-pointer hover:bg-muted">
                {wp.linkedFeatureRequestId}
              </Badge>
            </Link>
          </CardContent>
        </Card>
      )}

      {/* Timeline Card */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2.5">
            <Clock className="size-4" /> Timeline
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-6 text-sm">
            <div>
              <div className="text-sm text-muted-foreground mb-1">Started</div>
              <div>{formatDate(wp.startedAt)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Completed</div>
              <div>{formatDate(wp.completedAt)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Resolved</div>
              <div>{formatDate(wp.resolvedAt)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Created</div>
              <div>{formatDate(wp.createdAt)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground mb-1">Updated</div>
              <div>{formatDate(wp.updatedAt)}</div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Attachments Card */}
      {wp.attachments && wp.attachments.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Attachments</CardTitle>
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
                  {wp.attachments.map((att, idx) => (
                    <TableRow key={idx}>
                      <TableCell className="font-mono text-sm">{att.fileName}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {att.relativePath}
                      </TableCell>
                      <TableCell className="text-sm">{att.description ?? "\u2014"}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Phase / Task Tree Section */}
      <div className="space-y-4">
        <h2 className="text-lg font-semibold flex items-center gap-2">
          <Layers className="size-5" /> Phases & Tasks
        </h2>
        {!wp.phases || wp.phases.length === 0 ? (
          <div className="text-muted-foreground text-sm">No phases defined.</div>
        ) : (
          wp.phases.map((phase) => {
            const isExpanded = expandedPhases.has(phase.id);
            const taskCount = phase.tasks?.length ?? 0;

            return (
              <Card key={phase.id}>
                <CardHeader
                  className="cursor-pointer select-none"
                  onClick={() => togglePhase(phase.id)}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      {isExpanded ? (
                        <ChevronDown className="size-4" />
                      ) : (
                        <ChevronRight className="size-4" />
                      )}
                      <CardTitle className="text-base">{phase.name}</CardTitle>
                      <Badge variant="outline">Phase {phase.phaseNumber}</Badge>
                      <span
                        className={stateColorClass(phase.state)}
                      >
                        {phase.state}
                      </span>
                      <span className="text-xs text-muted-foreground">
                        {taskCount} task{taskCount !== 1 ? "s" : ""}
                      </span>
                    </div>
                    <Button
                      variant="ghost"
                      size="icon-xs"
                      onClick={(e) => {
                        e.stopPropagation();
                        setPhaseToDelete(phase);
                      }}
                    >
                      <Trash2 className="size-3.5" />
                    </Button>
                  </div>
                </CardHeader>
                {isExpanded && (
                  <CardContent className="space-y-4">
                    {phase.description && (
                      <div>
                        <div className="text-sm font-medium text-muted-foreground mb-1">
                          Description
                        </div>
                        <p className="text-sm whitespace-pre-wrap">{phase.description}</p>
                      </div>
                    )}

                    {/* Acceptance Criteria */}
                    {phase.acceptanceCriteria && phase.acceptanceCriteria.length > 0 && (
                      <div>
                        <div className="text-sm font-medium text-muted-foreground mb-2">
                          Acceptance Criteria
                        </div>
                        <ul className="space-y-1">
                          {phase.acceptanceCriteria.map((ac, idx) => (
                            <li key={idx} className="flex items-start gap-2 text-sm">
                              {ac.verifiedAt ? (
                                <CheckCircle2 className="size-4 text-green-600 mt-0.5 shrink-0" />
                              ) : (
                                <Circle className="size-4 text-gray-400 mt-0.5 shrink-0" />
                              )}
                              <span>{ac.description}</span>
                              {ac.verificationMethod && (
                                <Badge variant="outline" className="text-xs ml-1">
                                  {ac.verificationMethod}
                                </Badge>
                              )}
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                    {/* Tasks */}
                    {phase.tasks && phase.tasks.length > 0 ? (
                      <div>
                        <div className="text-sm font-medium text-muted-foreground mb-2">Tasks</div>
                        <div className="space-y-2">
                          {phase.tasks.map((task) => {
                            const taskKey = `${phase.id}-${task.sortOrder}`;
                            const isTaskExpanded = expandedTasks.has(taskKey);

                            return (
                              <div key={taskKey} className="border rounded-md">
                                <div
                                  className="flex items-center gap-2.5 px-3 py-3.5 cursor-pointer select-none"
                                  onClick={() => toggleTask(taskKey)}
                                >
                                  {isTaskExpanded ? (
                                    <ChevronDown className="size-4" />
                                  ) : (
                                    <ChevronRight className="size-4" />
                                  )}
                                  <span className="text-sm font-medium">{task.name}</span>
                                  {/* Task state select */}
                                  <Select
                                    value={task.state}
                                    onValueChange={(v) => {
                                      if (v !== task.state) handleTaskStateChange(task, v);
                                    }}
                                  >
                                    <SelectTrigger
                                      className={`w-auto h-auto py-0.5 px-2 text-xs border-0 ${stateColorClass(task.state)}`}
                                      onClick={(e) => e.stopPropagation()}
                                    >
                                      <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                      {completionStates.map((s) => (
                                        <SelectItem key={s} value={s}>{s}</SelectItem>
                                      ))}
                                    </SelectContent>
                                  </Select>
                                  {task.state === "Blocked" && task.previousActiveState && (
                                    <span className="text-xs text-muted-foreground">(was: {task.previousActiveState})</span>
                                  )}
                                  <Badge variant="outline" className="text-xs">
                                    #{task.sortOrder}
                                  </Badge>
                                  <Button
                                    variant="ghost"
                                    size="icon-xs"
                                    className="ml-auto"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      setTaskToDelete(task);
                                    }}
                                  >
                                    <Trash2 className="size-3.5" />
                                  </Button>
                                </div>
                                {isTaskExpanded && (
                                  <div className="px-3 pb-3 space-y-3 border-t pt-3">
                                    {task.description && (
                                      <div>
                                        <div className="text-sm font-medium text-muted-foreground mb-1">
                                          Description
                                        </div>
                                        <p className="text-sm whitespace-pre-wrap">
                                          {task.description}
                                        </p>
                                      </div>
                                    )}
                                    {task.implementationNotes && (
                                      <div>
                                        <div className="text-sm font-medium text-muted-foreground mb-1">
                                          Implementation Notes
                                        </div>
                                        <pre className="whitespace-pre-wrap text-sm bg-muted p-3 rounded-md">
                                          {task.implementationNotes}
                                        </pre>
                                      </div>
                                    )}
                                    {task.targetFiles && task.targetFiles.length > 0 && (
                                      <div>
                                        <div className="text-sm font-medium text-muted-foreground mb-1">
                                          Target Files
                                        </div>
                                        <ul className="space-y-1">
                                          {task.targetFiles.map((file, fIdx) => (
                                            <li
                                              key={fIdx}
                                              className="text-sm font-mono text-muted-foreground"
                                            >
                                              {file.relativePath}
                                            </li>
                                          ))}
                                        </ul>
                                      </div>
                                    )}
                                    {task.attachments && task.attachments.length > 0 && (
                                      <div>
                                        <div className="text-sm font-medium text-muted-foreground mb-1">
                                          Attachments
                                        </div>
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
                                              {task.attachments.map((att, aIdx) => (
                                                <TableRow key={aIdx}>
                                                  <TableCell className="font-mono text-sm">
                                                    {att.fileName}
                                                  </TableCell>
                                                  <TableCell className="text-sm text-muted-foreground">
                                                    {att.relativePath}
                                                  </TableCell>
                                                  <TableCell className="text-sm">
                                                    {att.description ?? "\u2014"}
                                                  </TableCell>
                                                </TableRow>
                                              ))}
                                            </TableBody>
                                          </Table>
                                        </div>
                                      </div>
                                    )}
                                    {task.blockedBy && task.blockedBy.length > 0 && (
                                      <div>
                                        <div className="text-sm font-medium text-muted-foreground mb-1">
                                          Blocked By
                                        </div>
                                        <div className="space-y-1">
                                          {task.blockedBy.map((dep: TaskDep, dIdx: number) => (
                                            <div
                                              key={dIdx}
                                              className="flex items-center gap-2 text-sm"
                                            >
                                              <span className="font-medium">{dep.name}</span>
                                              <span
                                                className={stateColorClass(dep.state)}
                                              >
                                                {dep.state}
                                              </span>
                                              {dep.reason && (
                                                <span className="text-muted-foreground">
                                                  - {dep.reason}
                                                </span>
                                              )}
                                            </div>
                                          ))}
                                        </div>
                                      </div>
                                    )}
                                  </div>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    ) : (
                      <div className="text-muted-foreground text-sm">No tasks in this phase.</div>
                    )}
                  </CardContent>
                )}
              </Card>
            );
          })
        )}
      </div>

      {/* State change confirmation dialog */}
      <AlertDialog open={!!stateToChange} onOpenChange={(open) => !open && setStateToChange(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Change work package state?</AlertDialogTitle>
            <AlertDialogDescription>
              Transition from <strong>{wp.state}</strong> to <strong>{stateToChange}</strong>.
              State-driven timestamps will be updated automatically.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleStateChange}>Confirm</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Phase Delete Dialog */}
      <AlertDialog
        open={!!phaseToDelete}
        onOpenChange={(open) => !open && setPhaseToDelete(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete phase?</AlertDialogTitle>
            <AlertDialogDescription>
              Delete phase <strong>{phaseToDelete?.name}</strong>? All tasks in this phase will also be deleted.
              This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handlePhaseDelete}
              className="bg-destructive text-white hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Task Delete Dialog */}
      <AlertDialog
        open={!!taskToDelete}
        onOpenChange={(open) => !open && setTaskToDelete(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete task?</AlertDialogTitle>
            <AlertDialogDescription>
              Delete task <strong>{taskToDelete?.name}</strong>?
              This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleTaskDelete}
              className="bg-destructive text-white hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete Dialog */}
      <AlertDialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete work package?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete <strong>{wp.name}</strong> ({wp.workPackageId}).
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
