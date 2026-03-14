import { projectHandlers } from "./handlers/project-handlers";
import { issueHandlers } from "./handlers/issue-handlers";
import { workPackageHandlers } from "./handlers/work-package-handlers";
import { featureRequestHandlers } from "./handlers/feature-request-handlers";
import { activityHandlers } from "./handlers/activity-handlers";
import { memoryHandlers } from "./handlers/memory-handlers";
import { authHandlers } from "./handlers/auth-handlers";

export const handlers = [
  ...authHandlers,
  ...projectHandlers,
  ...issueHandlers,
  ...workPackageHandlers,
  ...featureRequestHandlers,
  ...activityHandlers,
  ...memoryHandlers,
];
