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
