import { useState } from "react";
import { useParams, Link } from "react-router";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ArrowLeft, Trash2, User, Pencil, X, Save, Shield } from "lucide-react";
import { getUserById, updateUser, deactivateUser, getProjectRoles, assignRole, removeRole } from "@/api/users";
import { getProjects } from "@/api/projects";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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

const roleColors: Record<string, string> = {
  SuperUser: "bg-purple-500/15 text-purple-400 border-purple-500/30",
  User: "bg-gray-500/15 text-gray-400 border-gray-500/30",
};

export function UserDetailPage() {
  const { id: idParam } = useParams<{ id: string }>();
  const userId = Number(idParam);
  const queryClient = useQueryClient();

  const { data: user, isLoading } = useQuery({
    queryKey: ["user", userId],
    queryFn: () => getUserById(userId),
    enabled: !isNaN(userId),
  });

  const [isEditing, setIsEditing] = useState(false);
  const [showDeactivateDialog, setShowDeactivateDialog] = useState(false);
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editEmail, setEditEmail] = useState("");

  const updateMutation = useMutation({
    mutationFn: (data: { displayName?: string; email?: string; globalRole?: string }) =>
      updateUser(userId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["user", userId] });
      queryClient.invalidateQueries({ queryKey: ["users"] });
      setIsEditing(false);
      toast.success("User updated");
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : "Update failed"),
  });

  const deactivateMutation = useMutation({
    mutationFn: () => deactivateUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["user", userId] });
      queryClient.invalidateQueries({ queryKey: ["users"] });
      toast.success("User deactivated");
      setShowDeactivateDialog(false);
    },
  });

  function handleEdit() {
    if (!user) return;
    setEditDisplayName(user.displayName);
    setEditEmail(user.email);
    setIsEditing(true);
  }

  function handleSave() {
    updateMutation.mutate({
      displayName: editDisplayName,
      email: editEmail,
    });
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <p className="text-muted-foreground">Loading user...</p>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="flex items-center justify-center py-12">
        <p className="text-muted-foreground">User not found</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" asChild>
            <Link to="/users">
              <ArrowLeft className="size-5" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <User className="size-6" /> {user.displayName}
            </h1>
            <p className="text-sm text-muted-foreground">{user.email}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {isEditing ? (
            <>
              <Button size="sm" onClick={handleSave} disabled={updateMutation.isPending}>
                <Save className="size-4 mr-1" /> {updateMutation.isPending ? "Saving..." : "Save"}
              </Button>
              <Button variant="outline" size="sm" onClick={() => setIsEditing(false)}>
                <X className="size-4 mr-1" /> Cancel
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" size="sm" onClick={handleEdit}>
                <Pencil className="size-4 mr-1" /> Edit
              </Button>
              {user.isActive && (
                <Button
                  variant="outline"
                  size="sm"
                  className="text-destructive"
                  onClick={() => setShowDeactivateDialog(true)}
                >
                  <Trash2 className="size-4 mr-1" /> Deactivate
                </Button>
              )}
            </>
          )}
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>User Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="text-sm font-medium text-muted-foreground">Display Name</label>
              {isEditing ? (
                <Input
                  value={editDisplayName}
                  onChange={(e) => setEditDisplayName(e.target.value)}
                  className="mt-1"
                />
              ) : (
                <p className="mt-1">{user.displayName}</p>
              )}
            </div>
            <div>
              <label className="text-sm font-medium text-muted-foreground">Email</label>
              {isEditing ? (
                <Input
                  type="email"
                  value={editEmail}
                  onChange={(e) => setEditEmail(e.target.value)}
                  className="mt-1"
                />
              ) : (
                <p className="mt-1">{user.email}</p>
              )}
            </div>
            <div>
              <label className="text-sm font-medium text-muted-foreground">Role</label>
              <div className="mt-1">
                <Badge variant="outline" className={roleColors[user.globalRole] ?? ""}>
                  {user.globalRole}
                </Badge>
              </div>
            </div>
            <div>
              <label className="text-sm font-medium text-muted-foreground">Status</label>
              <div className="mt-1">
                <Badge
                  variant="outline"
                  className={
                    user.isActive
                      ? "bg-emerald-500/15 text-emerald-400 border-emerald-500/30"
                      : "bg-red-500/15 text-red-400 border-red-500/30"
                  }
                >
                  {user.isActive ? "Active" : "Inactive"}
                </Badge>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Project Roles */}
      <ProjectRolesSection userId={userId} />

      {/* Deactivate Dialog */}
      <AlertDialog open={showDeactivateDialog} onOpenChange={setShowDeactivateDialog}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Deactivate User</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to deactivate {user.displayName} ({user.email})?
              They will no longer be able to log in.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => deactivateMutation.mutate()}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Deactivate
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

// ── Project Roles Section ──

const roleOptions = ["No access", "Viewer", "Editor", "Admin"];

function ProjectRolesSection({ userId }: { userId: number }) {
  const queryClient = useQueryClient();

  const { data: projects } = useQuery({
    queryKey: ["projects"],
    queryFn: () => getProjects(),
  });

  // Build a map of projectId → role for this user
  const { data: roleMap, isLoading: rolesLoading } = useQuery({
    queryKey: ["user-project-roles", userId],
    queryFn: async () => {
      if (!projects?.length) return new Map<number, string>();
      const map = new Map<number, string>();
      await Promise.all(
        projects.map(async (project) => {
          const roles = await getProjectRoles(project.id);
          const userRole = roles.find((r) => r.userId === userId);
          if (userRole) map.set(project.id, userRole.role);
        }),
      );
      return map;
    },
    enabled: !!projects?.length,
  });

  const assignMutation = useMutation({
    mutationFn: ({ projectId, role }: { projectId: number; role: string }) =>
      assignRole(projectId, userId, role),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["user-project-roles", userId] });
      toast.success("Role updated");
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : "Failed to update role"),
  });

  const removeMutation = useMutation({
    mutationFn: (projectId: number) => removeRole(projectId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["user-project-roles", userId] });
      toast.success("Access removed");
    },
  });

  function handleRoleChange(projectId: number, newValue: string) {
    if (newValue === "No access") {
      removeMutation.mutate(projectId);
    } else {
      assignMutation.mutate({ projectId, role: newValue });
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Shield className="size-5" /> Project Roles
        </CardTitle>
      </CardHeader>
      <CardContent>
        {rolesLoading || !projects ? (
          <p className="text-sm text-muted-foreground">Loading projects...</p>
        ) : projects.length === 0 ? (
          <p className="text-sm text-muted-foreground">No projects exist yet.</p>
        ) : (
          <div className="space-y-2">
            {projects.map((project) => {
              const currentRole = roleMap?.get(project.id) ?? "No access";
              return (
                <div
                  key={project.id}
                  className="flex items-center justify-between rounded-md border p-3"
                >
                  <div className="min-w-0 flex-1">
                    <p className="font-medium truncate">{project.name}</p>
                    <p className="text-xs text-muted-foreground truncate">{project.projectPath}</p>
                  </div>
                  <Select
                    value={currentRole}
                    onValueChange={(value) => handleRoleChange(project.id, value)}
                  >
                    <SelectTrigger className="w-32 ml-4">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {roleOptions.map((r) => (
                        <SelectItem key={r} value={r}>{r}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
