import { useState } from "react";
import { useParams, useNavigate } from "react-router";
import { ArrowLeft, Trash2, Layers, ChevronDown, ChevronRight, CheckCircle2, Circle, Clock } from "lucide-react";
import { useWorkPackage, useDeleteWorkPackage } from "@/hooks/use-work-packages";
import type { TaskDep } from "@/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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

export function WorkPackageDetailPage() {
  const { id, wpNumber: wpNumParam } = useParams<{ id: string; wpNumber: string }>();
  const projectId = Number(id);
  const wpNumber = Number(wpNumParam);
  const navigate = useNavigate();

  const { data: wp, isLoading } = useWorkPackage(projectId, wpNumber);
  const deleteWorkPackage = useDeleteWorkPackage();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [expandedPhases, setExpandedPhases] = useState<Set<number>>(new Set());
  const [expandedTasks, setExpandedTasks] = useState<Set<string>>(new Set());

  const handleDelete = () => {
    deleteWorkPackage.mutate(
      { projectId, wpNumber },
      { onSuccess: () => navigate(`/projects/${projectId}`) },
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
        <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}`)}>
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
          <Button variant="ghost" size="sm" onClick={() => navigate(`/projects/${projectId}`)}>
            <ArrowLeft className="size-4" />
          </Button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold">{wp.name}</h1>
              <Badge variant="outline">{wp.workPackageId}</Badge>
              <Badge variant={typeVariant[wp.type] ?? "outline"}>{wp.type}</Badge>
              <Badge variant={priorityVariant[wp.priority] ?? "outline"}>{wp.priority}</Badge>
              <span
                className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ${stateColors[wp.state] ?? ""}`}
              >
                {wp.state}
              </span>
            </div>
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
            <Layers className="size-4" /> Definition
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <div className="text-xs font-medium text-muted-foreground mb-1">Description</div>
            <p className="text-sm whitespace-pre-wrap">{wp.description}</p>
          </div>
          {wp.plan && (
            <div>
              <div className="text-xs font-medium text-muted-foreground mb-1">Plan</div>
              <pre className="whitespace-pre-wrap text-sm bg-muted p-3 rounded-md">{wp.plan}</pre>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Estimation Card */}
      {hasEstimation && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Estimation</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {wp.estimatedComplexity != null && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">
                  Complexity (1-5)
                </div>
                <div className="text-sm font-medium">{wp.estimatedComplexity}</div>
              </div>
            )}
            {wp.estimationRationale && (
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Rationale</div>
                <p className="text-sm whitespace-pre-wrap">{wp.estimationRationale}</p>
              </div>
            )}
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
                <div className="text-xs font-medium text-muted-foreground mb-2">Blocked By</div>
                <div className="space-y-2">
                  {wp.blockedBy.map((dep, idx) => (
                    <div key={idx} className="flex items-center gap-2 text-sm">
                      <span className="font-medium">{dep.name}</span>
                      <span
                        className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ${stateColors[dep.state] ?? ""}`}
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
                <div className="text-xs font-medium text-muted-foreground mb-2">Blocking</div>
                <div className="space-y-2">
                  {wp.blocking.map((dep, idx) => (
                    <div key={idx} className="flex items-center gap-2 text-sm">
                      <span className="font-medium">{dep.name}</span>
                      <span
                        className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ${stateColors[dep.state] ?? ""}`}
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
            <Badge variant="outline">{wp.linkedIssueId}</Badge>
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
              <div className="text-xs text-muted-foreground mb-1">Started</div>
              <div>{formatDate(wp.startedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Completed</div>
              <div>{formatDate(wp.completedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Resolved</div>
              <div>{formatDate(wp.resolvedAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Created</div>
              <div>{formatDate(wp.createdAt)}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground mb-1">Updated</div>
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
                        className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ${stateColors[phase.state] ?? ""}`}
                      >
                        {phase.state}
                      </span>
                      <span className="text-xs text-muted-foreground">
                        {taskCount} task{taskCount !== 1 ? "s" : ""}
                      </span>
                    </div>
                  </div>
                </CardHeader>
                {isExpanded && (
                  <CardContent className="space-y-4">
                    {phase.description && (
                      <div>
                        <div className="text-xs font-medium text-muted-foreground mb-1">
                          Description
                        </div>
                        <p className="text-sm whitespace-pre-wrap">{phase.description}</p>
                      </div>
                    )}

                    {/* Acceptance Criteria */}
                    {phase.acceptanceCriteria && phase.acceptanceCriteria.length > 0 && (
                      <div>
                        <div className="text-xs font-medium text-muted-foreground mb-2">
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
                        <div className="text-xs font-medium text-muted-foreground mb-2">Tasks</div>
                        <div className="space-y-2">
                          {phase.tasks.map((task) => {
                            const taskKey = `${phase.id}-${task.sortOrder}`;
                            const isTaskExpanded = expandedTasks.has(taskKey);

                            return (
                              <div key={taskKey} className="border rounded-md">
                                <div
                                  className="flex items-center gap-2 p-3 cursor-pointer select-none"
                                  onClick={() => toggleTask(taskKey)}
                                >
                                  {isTaskExpanded ? (
                                    <ChevronDown className="size-3.5" />
                                  ) : (
                                    <ChevronRight className="size-3.5" />
                                  )}
                                  <span className="text-sm font-medium">{task.name}</span>
                                  <span
                                    className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ${stateColors[task.state] ?? ""}`}
                                  >
                                    {task.state}
                                  </span>
                                  <Badge variant="outline" className="text-xs">
                                    #{task.sortOrder}
                                  </Badge>
                                </div>
                                {isTaskExpanded && (
                                  <div className="px-3 pb-3 space-y-3 border-t pt-3">
                                    {task.description && (
                                      <div>
                                        <div className="text-xs font-medium text-muted-foreground mb-1">
                                          Description
                                        </div>
                                        <p className="text-sm whitespace-pre-wrap">
                                          {task.description}
                                        </p>
                                      </div>
                                    )}
                                    {task.implementationNotes && (
                                      <div>
                                        <div className="text-xs font-medium text-muted-foreground mb-1">
                                          Implementation Notes
                                        </div>
                                        <pre className="whitespace-pre-wrap text-sm bg-muted p-3 rounded-md">
                                          {task.implementationNotes}
                                        </pre>
                                      </div>
                                    )}
                                    {task.targetFiles && task.targetFiles.length > 0 && (
                                      <div>
                                        <div className="text-xs font-medium text-muted-foreground mb-1">
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
                                        <div className="text-xs font-medium text-muted-foreground mb-1">
                                          Attachments
                                        </div>
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
                                        <div className="text-xs font-medium text-muted-foreground mb-1">
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
                                                className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ${stateColors[dep.state] ?? ""}`}
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
