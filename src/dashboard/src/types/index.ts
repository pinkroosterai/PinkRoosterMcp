export interface PaginatedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface ActivityLog {
  id: number;
  httpMethod: string;
  path: string;
  statusCode: number;
  durationMs: number;
  callerIdentity: string | null;
  timestamp: string;
}

export interface Project {
  projectId: string;
  id: number;
  name: string;
  description: string;
  projectPath: string;
  status: "Active";
  createdAt: string;
  updatedAt: string;
}

export interface LinkedWorkPackageItem {
  workPackageId: string;
  name: string;
  state: string;
  type: string;
  priority: string;
}

export interface Issue {
  issueId: string;
  id: number;
  issueNumber: number;
  projectId: string;
  name: string;
  description: string;
  issueType: string;
  severity: "Critical" | "Major" | "Minor" | "Trivial";
  priority: "Critical" | "High" | "Medium" | "Low";
  stepsToReproduce: string | null;
  expectedBehavior: string | null;
  actualBehavior: string | null;
  affectedComponent: string | null;
  stackTrace: string | null;
  rootCause: string | null;
  resolution: string | null;
  state: string;
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  attachments: FileReference[];
  linkedWorkPackages: LinkedWorkPackageItem[];
  createdAt: string;
  updatedAt: string;
}

export interface FileReference {
  fileName: string;
  relativePath: string;
  description: string | null;
}

export interface IssueSummary {
  activeCount: number;
  inactiveCount: number;
  terminalCount: number;
  latestTerminalIssues: Issue[];
}

export interface IssueAuditLog {
  fieldName: string;
  oldValue: string | null;
  newValue: string | null;
  changedBy: string;
  changedAt: string;
}

export interface WorkPackage {
  workPackageId: string;
  id: number;
  workPackageNumber: number;
  projectId: string;
  name: string;
  description: string;
  type: "Feature" | "BugFix" | "Refactor" | "Spike" | "Chore";
  priority: "Critical" | "High" | "Medium" | "Low";
  plan: string | null;
  estimatedComplexity: number | null;
  estimationRationale: string | null;
  state: string;
  previousActiveState: string | null;
  linkedIssueIds: string[];
  linkedFeatureRequestIds: string[];
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  attachments: FileReference[];
  phases: Phase[];
  blockedBy: WorkPackageDep[];
  blocking: WorkPackageDep[];
  stateChanges?: StateChangeDto[] | null;
  createdAt: string;
  updatedAt: string;
}

export interface Phase {
  phaseId: string;
  id: number;
  phaseNumber: number;
  name: string;
  description: string | null;
  sortOrder: number;
  state: string;
  tasks: WpTask[];
  acceptanceCriteria: AcceptanceCriterionView[];
  createdAt: string;
  updatedAt: string;
}

export interface WpTask {
  taskId: string;
  id: number;
  taskNumber: number;
  phaseId: string;
  name: string;
  description: string;
  sortOrder: number;
  implementationNotes: string | null;
  state: string;
  previousActiveState: string | null;
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  targetFiles: FileReference[];
  attachments: FileReference[];
  blockedBy: TaskDep[];
  blocking: TaskDep[];
  stateChanges?: StateChangeDto[] | null;
  createdAt: string;
  updatedAt: string;
}

export interface AcceptanceCriterionView {
  name: string;
  description: string;
  verificationMethod: "AutomatedTest" | "Manual" | "AgentReview";
  verificationResult: string | null;
  verifiedAt: string | null;
}

export interface WorkPackageDep {
  workPackageId: string;
  name: string;
  state: string;
  reason: string | null;
}

export interface TaskDep {
  taskId: string;
  name: string;
  state: string;
  reason: string | null;
}

export interface WorkPackageSummary {
  activeCount: number;
  inactiveCount: number;
  terminalCount: number;
}

export interface UserStory {
  role: string;
  goal: string;
  benefit: string;
}

export interface FeatureRequest {
  featureRequestId: string;
  id: number;
  featureRequestNumber: number;
  projectId: string;
  name: string;
  description: string;
  category: string;
  priority: string;
  status: string;
  businessValue: string | null;
  userStories: UserStory[];
  requester: string | null;
  acceptanceSummary: string | null;
  startedAt: string | null;
  completedAt: string | null;
  resolvedAt: string | null;
  attachments: FileReference[];
  linkedWorkPackages: LinkedWorkPackageItem[];
  createdAt: string;
  updatedAt: string;
}

export interface StatusItem {
  id: string;
  name: string;
}

export interface EntityStatusSummary {
  total: number;
  active: number;
  inactive: number;
  terminal: number;
  percentComplete: number;
  activeItems: StatusItem[];
  inactiveItems: StatusItem[];
}

export interface WorkPackageStatusSummary {
  total: number;
  terminalCount: number;
  percentComplete: number;
  active: StatusItem[];
  inactive: StatusItem[];
  blocked: StatusItem[];
}

export interface MemoryStatusItem {
  memoryId: string;
  name: string;
  tags: string[];
}

export interface MemoryStatusSummary {
  total: number;
  recentMemories: MemoryStatusItem[];
  tagCloud: Record<string, number>;
}

export interface ProjectStatus {
  projectId: string;
  name: string;
  status: string;
  issues: EntityStatusSummary;
  featureRequests: EntityStatusSummary;
  workPackages: WorkPackageStatusSummary;
  memories: MemoryStatusSummary | null;
}

export interface ProjectMemoryListItem {
  memoryId: string;
  name: string;
  tags: string[];
  updatedAt: string;
}

export interface ProjectMemory {
  memoryId: string;
  projectId: string;
  memoryNumber: number;
  name: string;
  content: string;
  tags: string[];
  createdAt: string;
  updatedAt: string;
  wasMerged: boolean;
}

export interface StateChangeDto {
  entityType: string;
  entityId: string;
  oldState: string;
  newState: string;
  reason: string;
}

export interface NextActionItem {
  type: string;
  id: string;
  name: string;
  priority: string;
  state: string;
  parentId: string;
}
