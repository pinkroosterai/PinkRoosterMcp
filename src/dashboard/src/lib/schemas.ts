import { z } from "zod";

// Enum values matching the API exactly
export const issueTypes = ["Bug", "Defect", "Regression", "TechnicalDebt", "PerformanceIssue", "SecurityVulnerability"] as const;
export const issueSeverities = ["Critical", "Major", "Minor", "Trivial"] as const;
export const priorities = ["Critical", "High", "Medium", "Low"] as const;
export const completionStates = ["NotStarted", "Designing", "Implementing", "Testing", "InReview", "Completed", "Cancelled", "Blocked", "Replaced"] as const;
export const featureStatuses = ["Proposed", "UnderReview", "Approved", "Scheduled", "InProgress", "Completed", "Rejected", "Deferred"] as const;
export const featureCategories = ["Feature", "Enhancement", "Improvement"] as const;

// Issue schemas
export const createIssueSchema = z.object({
  name: z.string().min(1, "Name is required"),
  description: z.string().min(1, "Description is required"),
  issueType: z.enum(issueTypes, { message: "Issue type is required" }),
  severity: z.enum(issueSeverities, { message: "Severity is required" }),
  priority: z.enum(priorities).optional(),
  stepsToReproduce: z.string().optional(),
  expectedBehavior: z.string().optional(),
  actualBehavior: z.string().optional(),
  affectedComponent: z.string().optional(),
  stackTrace: z.string().optional(),
});

export type CreateIssueInput = z.infer<typeof createIssueSchema>;

export const updateIssueSchema = z.object({
  name: z.string().min(1, "Name is required").optional(),
  description: z.string().min(1, "Description is required").optional(),
  issueType: z.enum(issueTypes).optional(),
  severity: z.enum(issueSeverities).optional(),
  priority: z.enum(priorities).optional(),
  stepsToReproduce: z.string().optional(),
  expectedBehavior: z.string().optional(),
  actualBehavior: z.string().optional(),
  affectedComponent: z.string().optional(),
  stackTrace: z.string().optional(),
  rootCause: z.string().optional(),
  resolution: z.string().optional(),
});

export type UpdateIssueInput = z.infer<typeof updateIssueSchema>;

// User story schema
export const userStorySchema = z.object({
  role: z.string().min(1, "Role is required"),
  goal: z.string().min(1, "Goal is required"),
  benefit: z.string().min(1, "Benefit is required"),
});

export type UserStoryInput = z.infer<typeof userStorySchema>;

// Feature request schemas
export const createFeatureRequestSchema = z.object({
  name: z.string().min(1, "Name is required"),
  description: z.string().min(1, "Description is required"),
  category: z.enum(featureCategories, { message: "Category is required" }),
  priority: z.enum(priorities).optional(),
  businessValue: z.string().optional(),
  userStories: z.array(userStorySchema).optional(),
  requester: z.string().optional(),
  acceptanceSummary: z.string().optional(),
});

export type CreateFeatureRequestInput = z.infer<typeof createFeatureRequestSchema>;

export const updateFeatureRequestSchema = z.object({
  name: z.string().min(1, "Name is required").optional(),
  description: z.string().min(1, "Description is required").optional(),
  category: z.enum(featureCategories).optional(),
  priority: z.enum(priorities).optional(),
  businessValue: z.string().optional(),
  requester: z.string().optional(),
  acceptanceSummary: z.string().optional(),
});

export type UpdateFeatureRequestInput = z.infer<typeof updateFeatureRequestSchema>;

// Work package schemas
export const workPackageTypes = ["Feature", "BugFix", "Refactor", "Spike", "Chore"] as const;

export const createWorkPackageSchema = z.object({
  name: z.string().min(1, "Name is required"),
  description: z.string().min(1, "Description is required"),
  type: z.enum(workPackageTypes).optional(),
  priority: z.enum(priorities).optional(),
  plan: z.string().optional(),
  estimatedComplexity: z.union([z.number().min(1).max(10), z.nan()]).optional(),
  estimationRationale: z.string().optional(),
});

export type CreateWorkPackageInput = z.infer<typeof createWorkPackageSchema>;

export const updateWorkPackageSchema = z.object({
  name: z.string().min(1, "Name is required").optional(),
  description: z.string().min(1, "Description is required").optional(),
  type: z.enum(workPackageTypes).optional(),
  priority: z.enum(priorities).optional(),
  plan: z.string().optional(),
  estimatedComplexity: z.number().min(1).max(10).optional(),
  estimationRationale: z.string().optional(),
});

export type UpdateWorkPackageInput = z.infer<typeof updateWorkPackageSchema>;

// Auth schemas
export const registerSchema = z.object({
  email: z.string().email("Invalid email address"),
  password: z.string().min(8, "Password must be at least 8 characters").max(255),
  confirmPassword: z.string(),
  displayName: z.string().min(1, "Display name is required").max(200),
}).refine((data) => data.password === data.confirmPassword, {
  message: "Passwords do not match",
  path: ["confirmPassword"],
});

export type RegisterInput = z.infer<typeof registerSchema>;

export const loginSchema = z.object({
  email: z.string().email("Invalid email address"),
  password: z.string().min(1, "Password is required").max(255),
});

export type LoginInput = z.infer<typeof loginSchema>;

export const createUserSchema = z.object({
  email: z.string().email("Invalid email address"),
  displayName: z.string().min(1, "Display name is required").max(200),
  password: z.string().min(8, "Password must be at least 8 characters").max(255),
  confirmPassword: z.string(),
}).refine((data) => data.password === data.confirmPassword, {
  message: "Passwords do not match",
  path: ["confirmPassword"],
});

export type CreateUserInput = z.infer<typeof createUserSchema>;

export const changePasswordSchema = z.object({
  currentPassword: z.string().min(1, "Current password is required").max(255),
  newPassword: z.string().min(8, "New password must be at least 8 characters").max(255),
  confirmPassword: z.string(),
}).refine((data) => data.newPassword === data.confirmPassword, {
  message: "Passwords do not match",
  path: ["confirmPassword"],
});

export type ChangePasswordInput = z.infer<typeof changePasswordSchema>;
