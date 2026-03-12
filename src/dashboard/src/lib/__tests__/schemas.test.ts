import {
  createIssueSchema,
  updateIssueSchema,
  createFeatureRequestSchema,
  updateFeatureRequestSchema,
  createWorkPackageSchema,
  updateWorkPackageSchema,
  userStorySchema,
} from "../schemas";

describe("createIssueSchema", () => {
  const validIssue = {
    name: "Test Bug",
    description: "Something is broken",
    issueType: "Bug" as const,
    severity: "Major" as const,
  };

  it("accepts valid input with required fields only", () => {
    const result = createIssueSchema.safeParse(validIssue);
    expect(result.success).toBe(true);
  });

  it("accepts valid input with all optional fields", () => {
    const result = createIssueSchema.safeParse({
      ...validIssue,
      priority: "High",
      stepsToReproduce: "1. Open app",
      expectedBehavior: "Should work",
      actualBehavior: "Does not work",
      affectedComponent: "Dashboard",
      stackTrace: "Error at line 42",
    });
    expect(result.success).toBe(true);
  });

  it("rejects missing name", () => {
    const result = createIssueSchema.safeParse({ ...validIssue, name: "" });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].message).toBe("Name is required");
    }
  });

  it("rejects missing description", () => {
    const result = createIssueSchema.safeParse({ ...validIssue, description: "" });
    expect(result.success).toBe(false);
  });

  it("rejects invalid issueType", () => {
    const result = createIssueSchema.safeParse({ ...validIssue, issueType: "InvalidType" });
    expect(result.success).toBe(false);
  });

  it("rejects invalid severity", () => {
    const result = createIssueSchema.safeParse({ ...validIssue, severity: "Extreme" });
    expect(result.success).toBe(false);
  });

  it("rejects invalid priority", () => {
    const result = createIssueSchema.safeParse({ ...validIssue, priority: "Urgent" });
    expect(result.success).toBe(false);
  });

  it("accepts all valid issue types", () => {
    for (const issueType of ["Bug", "Defect", "Regression", "TechnicalDebt", "PerformanceIssue", "SecurityVulnerability"]) {
      const result = createIssueSchema.safeParse({ ...validIssue, issueType });
      expect(result.success).toBe(true);
    }
  });

  it("accepts all valid severities", () => {
    for (const severity of ["Critical", "Major", "Minor", "Trivial"]) {
      const result = createIssueSchema.safeParse({ ...validIssue, severity });
      expect(result.success).toBe(true);
    }
  });
});

describe("updateIssueSchema", () => {
  it("accepts empty object (no changes)", () => {
    const result = updateIssueSchema.safeParse({});
    expect(result.success).toBe(true);
  });

  it("accepts partial updates", () => {
    const result = updateIssueSchema.safeParse({ name: "Updated Name" });
    expect(result.success).toBe(true);
  });

  it("rejects empty name string", () => {
    const result = updateIssueSchema.safeParse({ name: "" });
    expect(result.success).toBe(false);
  });
});

describe("createFeatureRequestSchema", () => {
  const validFR = {
    name: "Dark Mode",
    description: "Add dark mode support",
    category: "Feature" as const,
  };

  it("accepts valid input with required fields only", () => {
    const result = createFeatureRequestSchema.safeParse(validFR);
    expect(result.success).toBe(true);
  });

  it("accepts valid input with all optional fields", () => {
    const result = createFeatureRequestSchema.safeParse({
      ...validFR,
      priority: "High",
      businessValue: "Improves UX",
      requester: "product-team",
      acceptanceSummary: "Toggle works",
      userStories: [{ role: "user", goal: "toggle dark mode", benefit: "reduced eye strain" }],
    });
    expect(result.success).toBe(true);
  });

  it("rejects missing name", () => {
    const result = createFeatureRequestSchema.safeParse({ ...validFR, name: "" });
    expect(result.success).toBe(false);
  });

  it("rejects missing description", () => {
    const result = createFeatureRequestSchema.safeParse({ ...validFR, description: "" });
    expect(result.success).toBe(false);
  });

  it("rejects invalid category", () => {
    const result = createFeatureRequestSchema.safeParse({ ...validFR, category: "Widget" });
    expect(result.success).toBe(false);
  });

  it("accepts all valid categories", () => {
    for (const category of ["Feature", "Enhancement", "Improvement"]) {
      const result = createFeatureRequestSchema.safeParse({ ...validFR, category });
      expect(result.success).toBe(true);
    }
  });

  it("rejects user story with empty role", () => {
    const result = createFeatureRequestSchema.safeParse({
      ...validFR,
      userStories: [{ role: "", goal: "do something", benefit: "something good" }],
    });
    expect(result.success).toBe(false);
  });
});

describe("userStorySchema", () => {
  it("accepts valid user story", () => {
    const result = userStorySchema.safeParse({ role: "user", goal: "do X", benefit: "get Y" });
    expect(result.success).toBe(true);
  });

  it("rejects missing role", () => {
    const result = userStorySchema.safeParse({ role: "", goal: "do X", benefit: "get Y" });
    expect(result.success).toBe(false);
  });

  it("rejects missing goal", () => {
    const result = userStorySchema.safeParse({ role: "user", goal: "", benefit: "get Y" });
    expect(result.success).toBe(false);
  });

  it("rejects missing benefit", () => {
    const result = userStorySchema.safeParse({ role: "user", goal: "do X", benefit: "" });
    expect(result.success).toBe(false);
  });
});

describe("createWorkPackageSchema", () => {
  const validWP = {
    name: "Implement Feature",
    description: "Build the new feature",
  };

  it("accepts valid input with required fields only", () => {
    const result = createWorkPackageSchema.safeParse(validWP);
    expect(result.success).toBe(true);
  });

  it("accepts valid input with all optional fields", () => {
    const result = createWorkPackageSchema.safeParse({
      ...validWP,
      type: "Feature",
      priority: "High",
      plan: "Step 1, Step 2",
      estimatedComplexity: 5,
      estimationRationale: "Moderate work",
    });
    expect(result.success).toBe(true);
  });

  it("rejects missing name", () => {
    const result = createWorkPackageSchema.safeParse({ ...validWP, name: "" });
    expect(result.success).toBe(false);
  });

  it("rejects invalid WP type", () => {
    const result = createWorkPackageSchema.safeParse({ ...validWP, type: "Invalid" });
    expect(result.success).toBe(false);
  });

  it("accepts all valid WP types", () => {
    for (const type of ["Feature", "BugFix", "Refactor", "Spike", "Chore"]) {
      const result = createWorkPackageSchema.safeParse({ ...validWP, type });
      expect(result.success).toBe(true);
    }
  });

  it("rejects complexity below 1", () => {
    const result = createWorkPackageSchema.safeParse({ ...validWP, estimatedComplexity: 0 });
    expect(result.success).toBe(false);
  });

  it("rejects complexity above 10", () => {
    const result = createWorkPackageSchema.safeParse({ ...validWP, estimatedComplexity: 11 });
    expect(result.success).toBe(false);
  });

  it("accepts complexity at boundaries (1 and 10)", () => {
    expect(createWorkPackageSchema.safeParse({ ...validWP, estimatedComplexity: 1 }).success).toBe(true);
    expect(createWorkPackageSchema.safeParse({ ...validWP, estimatedComplexity: 10 }).success).toBe(true);
  });
});

describe("updateWorkPackageSchema", () => {
  it("accepts empty object (no changes)", () => {
    const result = updateWorkPackageSchema.safeParse({});
    expect(result.success).toBe(true);
  });

  it("accepts partial updates", () => {
    const result = updateWorkPackageSchema.safeParse({ name: "Updated WP" });
    expect(result.success).toBe(true);
  });

  it("rejects complexity out of range", () => {
    expect(updateWorkPackageSchema.safeParse({ estimatedComplexity: 0 }).success).toBe(false);
    expect(updateWorkPackageSchema.safeParse({ estimatedComplexity: 11 }).success).toBe(false);
  });
});

describe("updateFeatureRequestSchema", () => {
  it("accepts empty object", () => {
    const result = updateFeatureRequestSchema.safeParse({});
    expect(result.success).toBe(true);
  });

  it("accepts partial updates", () => {
    const result = updateFeatureRequestSchema.safeParse({ name: "Updated FR", priority: "Low" });
    expect(result.success).toBe(true);
  });

  it("rejects empty name string", () => {
    const result = updateFeatureRequestSchema.safeParse({ name: "" });
    expect(result.success).toBe(false);
  });
});
