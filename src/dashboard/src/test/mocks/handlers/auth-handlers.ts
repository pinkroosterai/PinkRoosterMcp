import { http, HttpResponse } from "msw";

export const authHandlers = [
  http.get("/api/auth/config", () => {
    return HttpResponse.json({ isProtected: true });
  }),

  http.get("/api/auth/me", () => {
    return HttpResponse.json({
      id: 1,
      email: "test@example.com",
      displayName: "Test User",
      globalRole: "SuperUser",
      isActive: true,
    });
  }),

  http.get("/api/auth/me/permissions", () => {
    return HttpResponse.json({
      canRead: true,
      canCreate: true,
      canEdit: true,
      canDelete: true,
      canManageRoles: true,
      effectiveRole: "Admin",
    });
  }),
];
