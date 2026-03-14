import type { AuthUser } from "./auth";

const API_BASE = "/api";

export interface CreateUserData {
  email: string;
  password: string;
  displayName: string;
  globalRole: string;
}

export interface UpdateUserData {
  displayName?: string;
  email?: string;
  globalRole?: string;
  isActive?: boolean;
}

export interface UserProjectRole {
  userId: number;
  userEmail: string;
  userDisplayName: string;
  projectId: number;
  role: string;
  createdAt: string;
}

export async function getUsers(): Promise<AuthUser[]> {
  const res = await fetch(`${API_BASE}/users`, {
    credentials: "include",
  });
  if (!res.ok) throw new Error(`Failed to fetch users: ${res.status}`);
  return res.json();
}

export async function getUserById(id: number): Promise<AuthUser> {
  const res = await fetch(`${API_BASE}/users/${id}`, {
    credentials: "include",
  });
  if (!res.ok) throw new Error(`Failed to fetch user: ${res.status}`);
  return res.json();
}

export async function createUser(data: CreateUserData): Promise<AuthUser> {
  const res = await fetch(`${API_BASE}/users`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.detail ?? body?.message ?? "Failed to create user");
  }
  return res.json();
}

export async function updateUser(
  id: number,
  data: UpdateUserData,
): Promise<AuthUser> {
  const res = await fetch(`${API_BASE}/users/${id}`, {
    method: "PATCH",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!res.ok) throw new Error(`Failed to update user: ${res.status}`);
  return res.json();
}

export async function deactivateUser(id: number): Promise<void> {
  const res = await fetch(`${API_BASE}/users/${id}`, {
    method: "DELETE",
    credentials: "include",
  });
  if (!res.ok) throw new Error(`Failed to deactivate user: ${res.status}`);
}

export async function getProjectRoles(
  projectId: number,
): Promise<UserProjectRole[]> {
  const res = await fetch(`${API_BASE}/projects/${projectId}/roles`, {
    credentials: "include",
  });
  if (!res.ok) return [];
  return res.json();
}

export async function assignRole(
  projectId: number,
  userId: number,
  role: string,
): Promise<UserProjectRole> {
  const res = await fetch(`${API_BASE}/projects/${projectId}/roles/${userId}`, {
    method: "PUT",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ role }),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.detail ?? body?.error ?? "Failed to assign role");
  }
  return res.json();
}

export async function removeRole(
  projectId: number,
  userId: number,
): Promise<void> {
  const res = await fetch(`${API_BASE}/projects/${projectId}/roles/${userId}`, {
    method: "DELETE",
    credentials: "include",
  });
  if (!res.ok) throw new Error("Failed to remove role");
}
