import { useState } from "react";
import { useNavigate } from "react-router";
import { Trash2, FolderOpen } from "lucide-react";
import { useProjects, useDeleteProject } from "@/hooks/use-projects";
import { useProjectContext } from "@/hooks/use-project-context";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
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
import type { Project } from "@/types";

export function ProjectListPage() {
  const { data: projects, isLoading } = useProjects();
  const { setSelectedProject, selectedProject, clearSelectedProject } = useProjectContext();
  const deleteProject = useDeleteProject();
  const navigate = useNavigate();
  const [projectToDelete, setProjectToDelete] = useState<Project | null>(null);

  const handleSelect = (project: Project) => {
    setSelectedProject(project);
    navigate(`/projects/${project.id}`);
  };

  const handleDelete = () => {
    if (!projectToDelete) return;

    if (selectedProject?.id === projectToDelete.id) {
      clearSelectedProject();
    }

    deleteProject.mutate(projectToDelete.id, {
      onSettled: () => setProjectToDelete(null),
    });
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <h1 className="text-2xl font-bold">Projects</h1>
        <div className="text-muted-foreground">Loading...</div>
      </div>
    );
  }

  if (!projects?.length) {
    return (
      <div className="space-y-6">
        <h1 className="text-2xl font-bold">Projects</h1>
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <FolderOpen className="size-12 text-muted-foreground mb-4" />
            <h2 className="text-lg font-semibold">No projects yet</h2>
            <p className="text-sm text-muted-foreground mt-1 max-w-sm">
              Projects are created by AI agents via MCP tools. Once a project is
              registered, it will appear here.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Projects</h1>

      <div className="rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Project ID</TableHead>
              <TableHead>Name</TableHead>
              <TableHead className="hidden md:table-cell">Path</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="hidden sm:table-cell">Created</TableHead>
              <TableHead className="w-[60px]" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {projects.map((project) => (
              <TableRow
                key={project.id}
                className="cursor-pointer hover:bg-accent/50"
                onClick={() => handleSelect(project)}
              >
                <TableCell>
                  <Badge variant="outline">{project.projectId}</Badge>
                </TableCell>
                <TableCell className="font-medium">{project.name}</TableCell>
                <TableCell className="hidden md:table-cell max-w-[300px] truncate text-muted-foreground">
                  {project.projectPath}
                </TableCell>
                <TableCell>
                  <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${project.status === "Active" ? "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300" : "bg-gray-100 text-gray-700 dark:bg-gray-800/50 dark:text-gray-300"}`}>
                    {project.status}
                  </span>
                </TableCell>
                <TableCell className="hidden sm:table-cell text-muted-foreground">
                  {new Date(project.createdAt).toLocaleDateString()}
                </TableCell>
                <TableCell>
                  <Button
                    variant="ghost"
                    size="icon-xs"
                    onClick={(e) => {
                      e.stopPropagation();
                      setProjectToDelete(project);
                    }}
                  >
                    <Trash2 className="size-3.5" />
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <AlertDialog
        open={!!projectToDelete}
        onOpenChange={(open) => !open && setProjectToDelete(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete project?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete{" "}
              <strong>{projectToDelete?.name}</strong> (
              {projectToDelete?.projectId}). This action cannot be undone.
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
