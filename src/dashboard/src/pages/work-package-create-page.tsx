import { useState } from "react";
import { useParams, useNavigate, Link } from "react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowLeft, Layers } from "lucide-react";
import { toast } from "sonner";
import { useCreateWorkPackage } from "@/hooks/use-work-packages";
import { useIssues } from "@/hooks/use-issues";
import { useFeatureRequests } from "@/hooks/use-feature-requests";
import { createWorkPackageSchema, type CreateWorkPackageInput, workPackageTypes, priorities } from "@/lib/schemas";
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
import {
  Form,
  FormField,
  FormItem,
  FormLabel,
  FormControl,
  FormMessage,
} from "@/components/ui/form";

export function WorkPackageCreatePage() {
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const navigate = useNavigate();
  const createWp = useCreateWorkPackage();
  const { data: issues } = useIssues(projectId);
  const { data: featureRequests } = useFeatureRequests(projectId);

  const [linkedIssueId, setLinkedIssueId] = useState<string>("");
  const [linkedFrId, setLinkedFrId] = useState<string>("");

  const form = useForm<CreateWorkPackageInput>({
    resolver: zodResolver(createWorkPackageSchema),
    defaultValues: {
      name: "",
      description: "",
      type: "Feature",
      priority: "Medium",
      plan: "",
      estimationRationale: "",
    },
  });

  const onSubmit = (data: CreateWorkPackageInput) => {
    // valueAsNumber returns NaN for empty inputs — treat as undefined
    if (data.estimatedComplexity !== undefined && isNaN(data.estimatedComplexity)) {
      data.estimatedComplexity = undefined;
    }
    const payload: Record<string, unknown> = Object.fromEntries(
      Object.entries(data).filter(([, v]) => v !== "" && v !== undefined),
    );
    if (linkedIssueId) {
      payload.linkedIssueId = Number(linkedIssueId);
    }
    if (linkedFrId) {
      payload.linkedFeatureRequestId = Number(linkedFrId);
    }

    createWp.mutate(
      { projectId, data: payload },
      {
        onSuccess: (wp) => {
          toast.success(`Work package "${wp.name}" created`);
          navigate(`/projects/${projectId}/work-packages/${wp.workPackageNumber}`);
        },
        onError: (error) => {
          toast.error(`Failed to create work package: ${error.message}`);
        },
      },
    );
  };

  return (
    <div className="space-y-6 max-w-3xl">
      <div className="flex items-center gap-4 animate-in-right">
        <Button variant="ghost" size="icon" asChild>
          <Link to={`/projects/${projectId}/work-packages`}>
            <ArrowLeft className="size-4" />
          </Link>
        </Button>
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Layers className="size-6" /> Create Work Package
        </h1>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6 stagger-children">
          {/* Required fields */}
          <Card className="glass-card">
            <CardHeader>
              <CardTitle className="text-sm font-medium">Required</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Name</FormLabel>
                    <FormControl><Input placeholder="Work package title" {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="description"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Description</FormLabel>
                    <FormControl><Textarea placeholder="Describe the work package..." rows={4} {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <FormField
                  control={form.control}
                  name="type"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Type</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value ?? "Feature"}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue placeholder="Select type" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {workPackageTypes.map((t) => (
                            <SelectItem key={t} value={t}>{t}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="priority"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Priority</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value ?? "Medium"}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue placeholder="Select priority" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {priorities.map((p) => (
                            <SelectItem key={p} value={p}>{p}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
            </CardContent>
          </Card>

          {/* Optional details */}
          <Card className="glass-card">
            <CardHeader>
              <CardTitle className="text-sm font-medium">Details (Optional)</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <FormField
                control={form.control}
                name="plan"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Plan</FormLabel>
                    <FormControl><Textarea placeholder="Implementation plan or approach..." rows={3} {...field} /></FormControl>
                  </FormItem>
                )}
              />

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <label htmlFor="estimatedComplexity" className="text-sm font-medium leading-none">Estimated Complexity (1-10)</label>
                  <Input
                    id="estimatedComplexity"
                    type="number"
                    min={1}
                    max={10}
                    placeholder="e.g. 5"
                    {...form.register("estimatedComplexity", { valueAsNumber: true })}
                  />
                  {form.formState.errors.estimatedComplexity && (
                    <p className="text-xs font-medium text-destructive">{form.formState.errors.estimatedComplexity.message}</p>
                  )}
                </div>

                <FormField
                  control={form.control}
                  name="estimationRationale"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Estimation Rationale</FormLabel>
                      <FormControl><Input placeholder="Why this complexity?" {...field} /></FormControl>
                    </FormItem>
                  )}
                />
              </div>
            </CardContent>
          </Card>

          {/* Entity Linking */}
          <Card className="glass-card">
            <CardHeader>
              <CardTitle className="text-sm font-medium">Link Entities (Optional)</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">Link to Issue</label>
                  <Select onValueChange={setLinkedIssueId} value={linkedIssueId}>
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="No issue linked" />
                    </SelectTrigger>
                    <SelectContent>
                      {issues?.map((issue) => (
                        <SelectItem key={issue.id} value={String(issue.id)}>
                          {issue.issueId} — {issue.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <label className="text-sm font-medium">Link to Feature Request</label>
                  <Select onValueChange={setLinkedFrId} value={linkedFrId}>
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="No feature request linked" />
                    </SelectTrigger>
                    <SelectContent>
                      {featureRequests?.map((fr) => (
                        <SelectItem key={fr.id} value={String(fr.id)}>
                          {fr.featureRequestId} — {fr.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Actions */}
          <div className="flex items-center gap-3">
            <Button type="submit" disabled={createWp.isPending}>
              {createWp.isPending ? "Creating..." : "Create Work Package"}
            </Button>
            <Button type="button" variant="outline" onClick={() => navigate(`/projects/${projectId}/work-packages`)}>
              Cancel
            </Button>
          </div>
        </form>
      </Form>
    </div>
  );
}
