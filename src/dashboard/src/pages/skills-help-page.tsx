import {
  HelpCircle,
  BarChart3,
  Play,
  CheckCircle2,
  Hammer,
  Blocks,
  PenLine,
  ListFilter,
  Sparkles,
  Lightbulb,
  ShieldCheck,
  Trash2,
  ArrowRight,
  Terminal,
  Zap,
  MessageSquare,
  ScanSearch,
  Bug,
  Wrench,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";

interface SkillInfo {
  name: string;
  icon: React.ElementType;
  description: string;
  usage: string;
  args?: string;
  stateChanges: string[];
  readOnly?: boolean;
}

const skills: SkillInfo[] = [
  {
    name: "pm-status",
    icon: BarChart3,
    description:
      "Show a project status dashboard with issue, feature request, and work package counts, active and blocked items, and priority next actions.",
    usage: "/pm-status",
    args: "Optional: limit (number of next actions to show)",
    stateChanges: [],
    readOnly: true,
  },
  {
    name: "pm-next",
    icon: Play,
    description:
      "Pick up the next highest-priority actionable item and start implementing. Fetches full context, reads relevant code, and transitions to active state.",
    usage: "/pm-next [entityType]",
    args: "Optional: Task, Wp, Issue, or FeatureRequest to filter by entity type",
    stateChanges: [
      "Task \u2192 Implementing",
      "WP \u2192 Implementing (if inactive)",
      "Linked Issue \u2192 Implementing (if inactive)",
      "Linked FR \u2192 InProgress (if inactive)",
    ],
  },
  {
    name: "pm-done",
    icon: CheckCircle2,
    description:
      "Mark tasks, issues, or feature requests as completed and report cascading state changes. Supports single entity, multiple tasks, or batch mode.",
    usage: "/pm-done <id>",
    args: 'Single ID, multiple IDs (space-separated), or "all <wp-id>" for batch',
    stateChanges: [
      "Entity \u2192 Completed",
      "Phase auto-completes when all tasks are terminal",
      "WP auto-completes when all phases are terminal",
      "Linked Issue \u2192 Completed (on WP auto-complete)",
      "Linked FR \u2192 Completed (on WP auto-complete)",
    ],
  },
  {
    name: "pm-implement",
    icon: Hammer,
    description:
      "Execute implementation for a task, phase, or entire work package. Reads context, implements code changes, runs tests, and updates states automatically.",
    usage: "/pm-implement <id> [--dry-run]",
    args: "Task ID, Phase ID, or WP ID. Add --dry-run to preview the plan without executing",
    stateChanges: [
      "On start: Task \u2192 Implementing, WP \u2192 Implementing",
      "Linked Issue \u2192 Implementing, Linked FR \u2192 InProgress",
      "On finish: Task \u2192 Completed (phase/WP mode)",
      "Cascades same as pm-done on completion",
    ],
  },
  {
    name: "pm-scaffold",
    icon: Blocks,
    description:
      "Scaffold a complete work package with phases, tasks, dependencies, and target files. Analyzes the codebase to produce realistic implementation plans.",
    usage: "/pm-scaffold <description | issue-id | fr-id>",
    args: "Natural language description, or an Issue/FR ID to scaffold from",
    stateChanges: [
      "Linked Issue \u2192 Designing",
      "Linked FR \u2192 Scheduled",
    ],
  },
  {
    name: "pm-plan",
    icon: PenLine,
    description:
      "Create an issue or feature request from a natural language description. Classifies as bug or feature, confirms with you, then optionally scaffolds a work package.",
    usage: "/pm-plan <description>",
    args: "Natural language description of the work needed",
    stateChanges: [],
  },
  {
    name: "pm-triage",
    icon: ListFilter,
    description:
      "Review and prioritize open issues and feature requests. Analyzes severity, age, and codebase impact to recommend priority adjustments. Runs asynchronously.",
    usage: "/pm-triage",
    stateChanges: [],
    readOnly: true,
  },
  {
    name: "pm-refine-fr",
    icon: Sparkles,
    description:
      "Refine a feature request by analyzing its current state, rewriting the description with full detail, filling in missing fields (business value, acceptance summary), and adding structured user stories.",
    usage: "/pm-refine-fr <fr-id>",
    args: "Feature request ID (e.g., proj-1-fr-3). If omitted, lists inactive FRs to choose from",
    stateChanges: [],
  },
  {
    name: "pm-verify",
    icon: ShieldCheck,
    description:
      "Verify acceptance criteria for a phase or entire work package. Runs verification based on each criterion's method (AutomatedTest, Manual, AgentReview) and records results.",
    usage: "/pm-verify <phase-id | wp-id> [--dry-run]",
    args: "Phase ID or WP ID. Add --dry-run to preview without recording results",
    stateChanges: [],
  },
  {
    name: "pm-cleanup",
    icon: Trash2,
    description:
      "Analyze the codebase for dead code, unused imports, inconsistencies, and structural debt. Scaffolds a Chore work package with cleanup tasks that flow through the normal implement/test/commit pipeline.",
    usage: "/pm-cleanup [--dry-run] [--scope path]",
    args: "Optional: --dry-run to preview findings, --scope to limit analysis to a directory",
    stateChanges: [
      "Scaffolds a Chore WP (code changes via /pm-implement)",
    ],
  },
  {
    name: "pm-housekeeping",
    icon: Wrench,
    description:
      "Identify and remove stale, cancelled, rejected, or replaced PinkRooster entities. Scans for cleanup candidates, presents them for confirmation, and safely deletes selected items.",
    usage: "/pm-housekeeping [--dry-run]",
    args: "Optional: --dry-run to preview candidates without deleting",
    stateChanges: [],
  },
  {
    name: "pm-explore",
    icon: Lightbulb,
    description:
      "Analyze the codebase from a product manager's perspective and suggest realistic, user-facing feature enhancements. Cross-references existing items to avoid duplicates and creates selected suggestions as feature requests.",
    usage: "/pm-explore [--limit N]",
    args: "Optional: --limit N to cap suggestions (default 5)",
    stateChanges: [],
    readOnly: true,
  },
  {
    name: "pm-brainstorm",
    icon: MessageSquare,
    description:
      "Interactive feature brainstorming through guided discovery, web research, and codebase analysis. Helps explore vague ideas through Socratic dialogue before creating well-formed feature requests.",
    usage: "/pm-brainstorm [topic]",
    args: "Optional: topic or area to brainstorm (e.g., 'dashboard UX', 'notifications'). If omitted, discovers topic interactively",
    stateChanges: [],
  },
  {
    name: "pm-audit",
    icon: ScanSearch,
    description:
      "Proactive codebase audit using parallel analysis agents across quality, security, performance, and architecture domains. Discovers real problems and creates tracked issues from confirmed findings.",
    usage: "/pm-audit [--focus domain] [--scope path]",
    args: "Optional: --focus quality|security|performance|architecture, --scope path/to/dir. Supports natural language (e.g., 'check the API for security issues')",
    stateChanges: [],
  },
  {
    name: "pm-troubleshoot",
    icon: Bug,
    description:
      "Diagnose the root cause of a bug, error, crash, or unexpected behavior. Traces through code, logs, services, database state, and git history to find why something is broken. Researches error messages online for known issues.",
    usage: "/pm-troubleshoot <description>",
    args: "Description of the problem, error message, or stack trace",
    stateChanges: [],
    readOnly: true,
  },
];

export function SkillsHelpPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
          <HelpCircle className="h-6 w-6 text-muted-foreground" />
          PM Workflow Skills
        </h1>
        <p className="text-muted-foreground mt-1">
          Claude Code skills for AI-driven project management. Use these slash
          commands to plan, implement, and track work.
        </p>
      </div>

      <div className="grid gap-4">
        {skills.map((skill) => (
          <Card key={skill.name} className="glass-card">
            <CardHeader className="pb-3">
              <CardTitle className="flex items-center gap-2 text-lg">
                <skill.icon className="h-5 w-5 text-primary" />
                /{skill.name}
                {skill.readOnly && (
                  <Badge variant="secondary" className="text-xs font-normal">
                    Read-only
                  </Badge>
                )}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-sm text-muted-foreground">
                {skill.description}
              </p>

              <div className="flex items-center gap-2">
                <Terminal className="h-4 w-4 text-muted-foreground shrink-0" />
                <code className="text-sm bg-muted px-2 py-1 rounded font-mono">
                  {skill.usage}
                </code>
              </div>

              {skill.args && (
                <p className="text-xs text-muted-foreground ml-6">
                  {skill.args}
                </p>
              )}

              {skill.stateChanges.length > 0 && (
                <div className="ml-6 space-y-1">
                  <p className="text-xs font-medium flex items-center gap-1">
                    <Zap className="h-3 w-3 text-amber-500" />
                    Auto-State Propagation
                  </p>
                  <ul className="text-xs text-muted-foreground space-y-0.5">
                    {skill.stateChanges.map((change, i) => (
                      <li key={i} className="flex items-center gap-1.5">
                        <ArrowRight className="h-3 w-3 shrink-0" />
                        {change}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
