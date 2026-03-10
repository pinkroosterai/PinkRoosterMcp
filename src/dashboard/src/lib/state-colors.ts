/** Centralized state color mappings — works in both light and dark mode */

export const completionStateColors: Record<string, string> = {
  NotStarted: "bg-gray-100 text-gray-700 dark:bg-gray-800/50 dark:text-gray-300",
  Designing: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
  Implementing: "bg-indigo-100 text-indigo-700 dark:bg-indigo-900/40 dark:text-indigo-300",
  Testing: "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/40 dark:text-yellow-300",
  InReview: "bg-purple-100 text-purple-700 dark:bg-purple-900/40 dark:text-purple-300",
  Completed: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300",
  Cancelled: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300",
  Blocked: "bg-orange-100 text-orange-700 dark:bg-orange-900/40 dark:text-orange-300",
  Replaced: "bg-gray-200 text-gray-600 dark:bg-gray-800/50 dark:text-gray-400",
};

export const featureStatusColors: Record<string, string> = {
  Proposed: "bg-gray-100 text-gray-700 dark:bg-gray-800/50 dark:text-gray-300",
  UnderReview: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
  Approved: "bg-indigo-100 text-indigo-700 dark:bg-indigo-900/40 dark:text-indigo-300",
  Scheduled: "bg-purple-100 text-purple-700 dark:bg-purple-900/40 dark:text-purple-300",
  InProgress: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300",
  Completed: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300",
  Rejected: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300",
  Deferred: "bg-orange-100 text-orange-700 dark:bg-orange-900/40 dark:text-orange-300",
};

export function stateColorClass(state: string, type: "completion" | "feature" = "completion"): string {
  const map = type === "feature" ? featureStatusColors : completionStateColors;
  return `inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${map[state] ?? ""}`;
}

/** HTTP method badge colors for activity log */
export const methodColors: Record<string, string> = {
  GET: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300",
  POST: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
  PATCH: "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300",
  PUT: "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300",
  DELETE: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300",
};

/** HTTP status code colors */
export function statusCodeColor(code: number): string {
  if (code < 300) return "text-emerald-600 dark:text-emerald-400";
  if (code < 400) return "text-yellow-600 dark:text-yellow-400";
  return "text-red-600 dark:text-red-400";
}

/** Priority badge left-border accent colors */
export const priorityAccent: Record<string, string> = {
  Critical: "border-l-red-500",
  High: "border-l-orange-500",
  Medium: "border-l-blue-500",
  Low: "border-l-gray-400 dark:border-l-gray-600",
};
