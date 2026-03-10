/** Humanize raw API paths into readable descriptions */
export function humanizePath(path: string): string {
  // /api/projects/1/issues/7 → "Issue #7"
  const issueMatch = path.match(/\/projects\/\d+\/issues\/(\d+)$/);
  if (issueMatch) return `Issue #${issueMatch[1]}`;

  // /api/projects/1/issues → "Issues list"
  if (/\/projects\/\d+\/issues$/.test(path)) return "Issues list";

  // /api/projects/1/issues/summary → "Issues summary"
  if (/\/projects\/\d+\/issues\/summary$/.test(path)) return "Issues summary";

  // /api/projects/1/feature-requests/3 → "Feature Request #3"
  const frMatch = path.match(/\/projects\/\d+\/feature-requests\/(\d+)$/);
  if (frMatch) return `Feature Request #${frMatch[1]}`;

  // /api/projects/1/feature-requests → "Feature Requests list"
  if (/\/projects\/\d+\/feature-requests$/.test(path)) return "Feature Requests list";

  // /api/projects/1/work-packages/5 → "Work Package #5"
  const wpMatch = path.match(/\/projects\/\d+\/work-packages\/(\d+)$/);
  if (wpMatch) return `Work Package #${wpMatch[1]}`;

  // /api/projects/1/work-packages → "Work Packages list"
  if (/\/projects\/\d+\/work-packages$/.test(path)) return "Work Packages list";

  // /api/projects/1/work-packages/summary → "Work Packages summary"
  if (/\/projects\/\d+\/work-packages\/summary$/.test(path)) return "WP summary";

  // /api/projects/1/work-packages/5/phases/2/tasks/1 → "Task in WP #5"
  const taskMatch = path.match(/\/work-packages\/(\d+)\/phases\/\d+\/tasks/);
  if (taskMatch) return `Task in WP #${taskMatch[1]}`;

  // /api/projects/1/work-packages/5/phases → "Phases in WP #5"
  const phaseMatch = path.match(/\/work-packages\/(\d+)\/phases/);
  if (phaseMatch) return `Phases in WP #${phaseMatch[1]}`;

  // /api/projects/1 → "Project #1"
  const projMatch = path.match(/\/projects\/(\d+)$/);
  if (projMatch) return `Project #${projMatch[1]}`;

  // /api/projects → "Projects list"
  if (/\/projects$/.test(path)) return "Projects list";

  // /api/projects/1/next-actions → "Next actions"
  if (/\/next-actions/.test(path)) return "Next actions";

  // /api/activity-logs → "Activity logs"
  if (/\/activity-logs/.test(path)) return "Activity logs";

  // /api/health → "Health check"
  if (/\/health/.test(path)) return "Health check";

  // Fallback: strip /api/ prefix
  return path.replace(/^\/api\//, "");
}
