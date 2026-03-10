import { useParams, useNavigate, Link } from "react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowLeft, Lightbulb } from "lucide-react";
import { toast } from "sonner";
import { useCreateFeatureRequest } from "@/hooks/use-feature-requests";
import { createFeatureRequestSchema, type CreateFeatureRequestInput, featureCategories, priorities } from "@/lib/schemas";
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

export function FeatureRequestCreatePage() {
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const navigate = useNavigate();
  const createFr = useCreateFeatureRequest();

  const form = useForm<CreateFeatureRequestInput>({
    resolver: zodResolver(createFeatureRequestSchema),
    defaultValues: {
      name: "",
      description: "",
      priority: "Medium",
      businessValue: "",
      userStory: "",
      requester: "",
      acceptanceSummary: "",
    },
  });

  const onSubmit = (data: CreateFeatureRequestInput) => {
    const payload = Object.fromEntries(
      Object.entries(data).filter(([, v]) => v !== "" && v !== undefined),
    );

    createFr.mutate(
      { projectId, data: payload },
      {
        onSuccess: (fr) => {
          toast.success(`Feature request "${fr.name}" created`);
          navigate(`/projects/${projectId}/feature-requests/${fr.featureRequestNumber}`);
        },
        onError: (error) => {
          toast.error(`Failed to create feature request: ${error.message}`);
        },
      },
    );
  };

  return (
    <div className="space-y-6 max-w-3xl">
      <div className="flex items-center gap-4 animate-in-right">
        <Button variant="ghost" size="icon" asChild>
          <Link to={`/projects/${projectId}/feature-requests`}>
            <ArrowLeft className="size-4" />
          </Link>
        </Button>
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Lightbulb className="size-6" /> Create Feature Request
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
                    <FormControl><Input placeholder="Feature request title" {...field} /></FormControl>
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
                    <FormControl><Textarea placeholder="Describe the feature..." rows={4} {...field} /></FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <FormField
                  control={form.control}
                  name="category"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Category</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value}>
                        <FormControl>
                          <SelectTrigger className="w-full">
                            <SelectValue placeholder="Select category" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {featureCategories.map((c) => (
                            <SelectItem key={c} value={c}>{c}</SelectItem>
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
                name="businessValue"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Business Value</FormLabel>
                    <FormControl><Textarea placeholder="Why is this valuable?" rows={2} {...field} /></FormControl>
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="userStory"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>User Story</FormLabel>
                    <FormControl><Textarea placeholder="As a [user], I want [goal] so that [benefit]" rows={2} {...field} /></FormControl>
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="requester"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Requester</FormLabel>
                    <FormControl><Input placeholder="Who requested this?" {...field} /></FormControl>
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="acceptanceSummary"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Acceptance Summary</FormLabel>
                    <FormControl><Textarea placeholder="How will we know this is done?" rows={2} {...field} /></FormControl>
                  </FormItem>
                )}
              />
            </CardContent>
          </Card>

          {/* Actions */}
          <div className="flex items-center gap-3">
            <Button type="submit" disabled={createFr.isPending}>
              {createFr.isPending ? "Creating..." : "Create Feature Request"}
            </Button>
            <Button type="button" variant="outline" onClick={() => navigate(`/projects/${projectId}/feature-requests`)}>
              Cancel
            </Button>
          </div>
        </form>
      </Form>
    </div>
  );
}
