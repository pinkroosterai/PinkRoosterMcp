export interface AuthConfig {
  isProtected: boolean;
}

export interface AuthUser {
  id: number;
  email: string;
  displayName: string;
  globalRole: string;
  isActive: boolean;
}

export interface LoginResponse {
  user: AuthUser;
  expiresAt: string;
}

const API_BASE = "/api";

export async function checkAuthConfig(): Promise<AuthConfig> {
  const res = await fetch(`${API_BASE}/auth/config`, {
    credentials: "include",
  });
  return res.json();
}

export async function login(
  email: string,
  password: string,
): Promise<LoginResponse> {
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.message ?? "Login failed");
  }

  return res.json();
}

export async function register(
  email: string,
  password: string,
  displayName: string,
): Promise<AuthUser> {
  const res = await fetch(`${API_BASE}/auth/register`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password, displayName }),
  });

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.message ?? "Registration failed");
  }

  return res.json();
}

export async function logout(): Promise<void> {
  await fetch(`${API_BASE}/auth/logout`, {
    method: "POST",
    credentials: "include",
  });
}

export interface UserPermissions {
  effectiveRole: string;
  canRead: boolean;
  canCreate: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canManageRoles: boolean;
}

export async function getMyPermissions(
  projectId: number,
): Promise<UserPermissions> {
  const res = await fetch(
    `${API_BASE}/auth/me/permissions?projectId=${projectId}`,
    { credentials: "include" },
  );

  if (!res.ok) {
    return {
      effectiveRole: "None",
      canRead: false,
      canCreate: false,
      canEdit: false,
      canDelete: false,
      canManageRoles: false,
    };
  }

  return res.json();
}

export async function updateProfile(data: {
  displayName?: string;
  email?: string;
  currentPassword?: string;
}): Promise<AuthUser> {
  const res = await fetch(`${API_BASE}/auth/me`, {
    method: "PATCH",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.message ?? "Failed to update profile");
  }
  return res.json();
}

export async function changePassword(data: {
  currentPassword: string;
  newPassword: string;
}): Promise<void> {
  const res = await fetch(`${API_BASE}/auth/me/password`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.message ?? "Failed to change password");
  }
}

export async function getCurrentUser(): Promise<AuthUser | null> {
  const res = await fetch(`${API_BASE}/auth/me`, {
    credentials: "include",
  });

  if (!res.ok) return null;
  return res.json();
}
