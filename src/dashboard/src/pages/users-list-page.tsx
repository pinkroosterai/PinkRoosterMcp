import { useNavigate, Link } from "react-router";
import { type ColumnDef } from "@tanstack/react-table";
import { Users, Plus } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { getUsers } from "@/api/users";
import { PageTransition } from "@/components/page-transition";
import { TableSkeleton } from "@/components/loading-skeletons";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { DataTable } from "@/components/data-table";
import type { AuthUser } from "@/api/auth";

const roleColors: Record<string, string> = {
  SuperUser: "bg-purple-500/15 text-purple-400 border-purple-500/30",
  Admin: "bg-blue-500/15 text-blue-400 border-blue-500/30",
  Editor: "bg-green-500/15 text-green-400 border-green-500/30",
  Viewer: "bg-gray-500/15 text-gray-400 border-gray-500/30",
};

const columns: ColumnDef<AuthUser>[] = [
  {
    accessorKey: "email",
    header: "Email",
    cell: ({ row }) => (
      <span className="font-medium">{row.original.email}</span>
    ),
  },
  {
    accessorKey: "displayName",
    header: "Display Name",
  },
  {
    accessorKey: "globalRole",
    header: "Role",
    cell: ({ row }) => (
      <Badge variant="outline" className={roleColors[row.original.globalRole] ?? ""}>
        {row.original.globalRole}
      </Badge>
    ),
  },
  {
    accessorKey: "isActive",
    header: "Status",
    cell: ({ row }) => (
      <Badge
        variant="outline"
        className={
          row.original.isActive
            ? "bg-emerald-500/15 text-emerald-400 border-emerald-500/30"
            : "bg-red-500/15 text-red-400 border-red-500/30"
        }
      >
        {row.original.isActive ? "Active" : "Inactive"}
      </Badge>
    ),
  },
];

export function UsersListPage() {
  const navigate = useNavigate();
  const { data: users, isLoading } = useQuery({
    queryKey: ["users"],
    queryFn: getUsers,
  });

  if (isLoading) {
    return <TableSkeleton rows={5} columns={4} />;
  }

  return (
    <PageTransition>
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2 animate-in-right">
          <Users className="size-6" /> Users
        </h1>
        <Button asChild>
          <Link to="/users/new">
            <Plus className="size-4 mr-1.5" /> Create User
          </Link>
        </Button>
      </div>

      {!users?.length ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <Users className="size-12 text-muted-foreground mb-4" />
            <h2 className="text-lg font-semibold">No users found</h2>
            <p className="text-sm text-muted-foreground mt-1 mb-4 max-w-sm">
              Create your first user to get started.
            </p>
            <Button asChild>
              <Link to="/users/new">
                <Plus className="size-4 mr-1.5" /> Create User
              </Link>
            </Button>
          </CardContent>
        </Card>
      ) : (
        <DataTable
          columns={columns}
          data={users}
          onRowClick={(user) => navigate(`/users/${user.id}`)}
        />
      )}
    </div>
    </PageTransition>
  );
}
