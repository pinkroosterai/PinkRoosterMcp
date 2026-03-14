import { useQuery } from "@tanstack/react-query";
import { getMyPermissions } from "@/api/auth";
import { useAuth } from "@/components/auth-provider";

export function usePermissions(projectId: number | undefined) {
  const { isAuthenticated } = useAuth();

  const { data, isLoading } = useQuery({
    queryKey: ["permissions", projectId],
    queryFn: () => getMyPermissions(projectId!),
    enabled: projectId !== undefined && isAuthenticated,
    staleTime: 60_000,
  });

  return {
    canRead: data?.canRead ?? false,
    canCreate: data?.canCreate ?? false,
    canEdit: data?.canEdit ?? false,
    canDelete: data?.canDelete ?? false,
    canManageRoles: data?.canManageRoles ?? false,
    effectiveRole: data?.effectiveRole ?? "None",
    isLoading,
  };
}
