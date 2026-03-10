import { projectHandlers } from "./handlers/project-handlers";
import { issueHandlers } from "./handlers/issue-handlers";
import { workPackageHandlers } from "./handlers/work-package-handlers";
import { featureRequestHandlers } from "./handlers/feature-request-handlers";
import { activityHandlers } from "./handlers/activity-handlers";

export const handlers = [
  ...projectHandlers,
  ...issueHandlers,
  ...workPackageHandlers,
  ...featureRequestHandlers,
  ...activityHandlers,
];
