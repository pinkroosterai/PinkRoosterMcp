# Research Report: How to Write Skills for Claude Code

**Date**: 2026-03-10
**Depth**: Deep (3-4 hops)
**Confidence**: High (official documentation + community validation)

---

## Executive Summary

Claude Code skills are markdown-based instruction packages that extend Claude's capabilities. They follow the [Agent Skills open standard](https://agentskills.io) (adopted by 30+ AI tools) and are the evolution of the older `.claude/commands/` system. Skills consist of a `SKILL.md` file with YAML frontmatter and markdown instructions, optionally supported by scripts, templates, and reference docs. They can be auto-triggered by Claude, manually invoked via `/skill-name`, or both. Advanced patterns include subagent execution (`context: fork`), dynamic shell injection (`!`command``), model overrides, and scoped hooks.

---

## 1. Anatomy of a Skill

### 1.1 Directory Structure

```
.claude/skills/my-skill/
├── SKILL.md           # Required: frontmatter + instructions
├── template.md        # Optional: template for Claude to fill in
├── examples/
│   └── sample.md      # Optional: example output
├── references/
│   └── REFERENCE.md   # Optional: detailed technical reference
├── scripts/
│   └── helper.py      # Optional: executable script
└── assets/
    └── schema.json    # Optional: static resources
```

### 1.2 SKILL.md Format

```yaml
---
name: my-skill                        # Lowercase, hyphens, max 64 chars
description: What it does and when    # Max 1024 chars (recommended)
argument-hint: [issue-number]         # Shown during autocomplete
disable-model-invocation: false       # true = user-only trigger
user-invocable: true                  # false = Claude-only (hidden from / menu)
allowed-tools: Read, Grep, Glob      # Tools allowed without permission prompts
model: sonnet                         # Model override (optional)
context: fork                         # Run in isolated subagent
agent: Explore                        # Subagent type (when context: fork)
hooks: {}                             # Lifecycle hooks (skill-scoped)
---

Your markdown instructions here...
```

### 1.3 Frontmatter Reference

| Field | Required | Description |
|-------|----------|-------------|
| `name` | No (uses dir name) | Lowercase letters, numbers, hyphens only. Max 64 chars. Must match directory name per Agent Skills spec. |
| `description` | Recommended | What the skill does + when to use it. Claude uses this for auto-triggering. |
| `argument-hint` | No | Autocomplete hint (e.g., `[filename] [format]`) |
| `disable-model-invocation` | No | `true` = only user can invoke. Default: `false` |
| `user-invocable` | No | `false` = hidden from `/` menu. Default: `true` |
| `allowed-tools` | No | Comma-separated tools allowed without prompts |
| `model` | No | Model override when skill is active |
| `context` | No | `fork` = run in isolated subagent context |
| `agent` | No | Subagent type: `Explore`, `Plan`, `general-purpose`, or custom from `.claude/agents/` |
| `hooks` | No | Lifecycle hooks scoped to this skill |

### 1.4 String Substitutions

| Variable | Description |
|----------|-------------|
| `$ARGUMENTS` | All arguments passed when invoking |
| `$ARGUMENTS[N]` | Specific argument by 0-based index |
| `$N` | Shorthand for `$ARGUMENTS[N]` (e.g., `$0`, `$1`) |
| `${CLAUDE_SESSION_ID}` | Current session ID |
| `${CLAUDE_SKILL_DIR}` | Directory containing the SKILL.md file |

---

## 2. Where Skills Live (Priority Order)

| Location | Path | Scope |
|----------|------|-------|
| Enterprise | Managed settings | All org users |
| Personal | `~/.claude/skills/<name>/SKILL.md` | All your projects |
| Project | `.claude/skills/<name>/SKILL.md` | This project only |
| Plugin | `<plugin>/skills/<name>/SKILL.md` | Where plugin enabled |

**Precedence**: Enterprise > Personal > Project. Plugin skills use `plugin-name:skill-name` namespace (no conflicts).

**Monorepo support**: Nested `.claude/skills/` directories are auto-discovered when editing files in subdirectories (e.g., `packages/frontend/.claude/skills/`).

**Legacy**: `.claude/commands/` files still work identically but skills take precedence on name conflicts.

---

## 3. Invocation Control Matrix

| Frontmatter | User invokes | Claude invokes | Context loading |
|-------------|-------------|----------------|-----------------|
| (default) | Yes | Yes | Description always in context; full skill loads on invoke |
| `disable-model-invocation: true` | Yes | No | Description NOT in context; loads only on user invoke |
| `user-invocable: false` | No | Yes | Description always in context; loads on Claude invoke |

---

## 4. Skill Content Types

### 4.1 Reference Content (Knowledge)
Adds domain knowledge Claude applies to current work. Runs inline.

```yaml
---
name: api-conventions
description: API design patterns for this codebase
---
When writing API endpoints:
- Use RESTful naming conventions
- Return consistent error formats
- Include request validation
```

### 4.2 Task Content (Actions)
Step-by-step instructions for specific actions. Usually manually invoked.

```yaml
---
name: deploy
description: Deploy the application to production
disable-model-invocation: true
context: fork
---
Deploy $ARGUMENTS to production:
1. Run the test suite
2. Build the application
3. Push to the deployment target
4. Verify the deployment succeeded
```

---

## 5. Advanced Patterns

### 5.1 Dynamic Context Injection

The `!`command`` syntax runs shell commands before Claude sees the skill content. Output replaces the placeholder.

```yaml
---
name: pr-summary
description: Summarize changes in a pull request
context: fork
agent: Explore
allowed-tools: Bash(gh *)
---
## Pull request context
- PR diff: !`gh pr diff`
- PR comments: !`gh pr view --comments`
- Changed files: !`gh pr diff --name-only`

## Your task
Summarize this pull request...
```

### 5.2 Subagent Execution (context: fork)

Run skills in isolated contexts without conversation history.

```yaml
---
name: deep-research
description: Research a topic thoroughly
context: fork
agent: Explore
---
Research $ARGUMENTS thoroughly:
1. Find relevant files using Glob and Grep
2. Read and analyze the code
3. Summarize findings with specific file references
```

**When to use `context: fork`**:
- Tasks that need clean context (no conversation history bleed)
- Long-running operations that shouldn't block the main conversation
- Read-only research that benefits from specialized agent types
- Tasks with side effects you want to isolate

**Warning**: `context: fork` only works with explicit task instructions. Guidelines-only content (e.g., "use these conventions") without a task produces no meaningful output.

### 5.3 Extended Thinking

Include the word **"ultrathink"** anywhere in skill content to enable extended thinking mode.

### 5.4 Visual Output Generation

Bundle scripts that generate interactive HTML output:

```yaml
---
name: codebase-visualizer
description: Generate interactive tree visualization of your codebase
allowed-tools: Bash(python *)
---
# Codebase Visualizer
Run the visualization script from your project root:
```bash
python ${CLAUDE_SKILL_DIR}/scripts/visualize.py .
```
```

### 5.5 Permission Restriction Patterns

```
# Allow only specific skills in /permissions
Skill(commit)
Skill(review-pr *)

# Deny specific skills
Skill(deploy *)

# Deny all skills
Skill
```

---

## 6. Best Practices

### 6.1 Skill Design

1. **One skill per workflow** — A focused `/security-review` beats a "do everything" skill. Three focused skills > one monolithic skill.

2. **Keep SKILL.md under 500 lines** — Move detailed reference material to supporting files. The body loads fully when invoked.

3. **Write descriptions for matching** — Include keywords users would naturally say. The description is the primary auto-trigger mechanism.

4. **Use imperative verbs** — "Review the code", "Write tests", "Create a migration". Skills are instructions, not conversations.

5. **Define output format explicitly** — Tell Claude exactly how to structure responses: bullet points, tables, code blocks, etc.

6. **Use descriptive names** — `/security-review` > `/sr`. Max 64 chars, lowercase + hyphens only.

### 6.2 Context Budget Management

- Skill descriptions consume context proportional to 2% of the context window (fallback: 16,000 chars)
- Run `/context` to check if skills are being excluded
- Override with `SLASH_COMMAND_TOOL_CHAR_BUDGET` env var
- Use `disable-model-invocation: true` for rarely-used skills (removes description from context entirely)

### 6.3 Structuring Instructions

```yaml
---
name: code-review
description: Review code for bugs, security, and style issues
disable-model-invocation: true
argument-hint: [file-or-directory]
---

## Scope
Review $ARGUMENTS for the following categories.

## Checklist
1. **Security**: Check for injection, XSS, auth bypass
2. **Bugs**: Look for null derefs, off-by-one, race conditions
3. **Style**: Verify naming conventions, formatting
4. **Performance**: Identify N+1 queries, unnecessary allocations

## Output Format
For each finding:
- **File**: path:line
- **Severity**: Critical / Warning / Info
- **Issue**: One-line description
- **Fix**: Suggested code change

## Constraints
- Only report issues you're confident about
- Skip generated files and test fixtures
```

### 6.4 Supporting Files Strategy

```
my-skill/
├── SKILL.md          # Overview + navigation (< 500 lines)
├── reference.md      # Detailed API docs (loaded when needed)
├── examples.md       # Usage examples (loaded when needed)
└── scripts/
    └── helper.py     # Utility script (executed, not loaded)
```

Reference from SKILL.md:
```markdown
## Additional resources
- For complete API details, see [reference.md](reference.md)
- For usage examples, see [examples.md](examples.md)
```

### 6.5 Arguments Best Practices

- Always include `$ARGUMENTS` in the skill body so placement is intentional
- If `$ARGUMENTS` is missing, Claude Code appends `ARGUMENTS: <value>` at the end
- Use `$ARGUMENTS[N]` or `$N` for multi-argument skills
- Add `argument-hint` for autocomplete UX

### 6.6 Security Considerations

- **Audit skill definitions** — Skills can be vectors for prompt injection if imported from untrusted sources
- **Restrict allowed-tools** — Only grant tools the skill actually needs
- **Use `disable-model-invocation: true`** for skills with side effects (deploy, send messages, delete)
- **Never store secrets** in skill files — Use environment variables or `.env` files instead

---

## 7. Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|-------------|-------------|-----------------|
| Mega-skill that does everything | Less effective, confuses auto-triggering | Split into focused skills |
| Extensive custom command lists | Users can't memorize undocumented commands | Keep minimal; use good descriptions |
| Negative-only constraints ("Never X") | Causes agent paralysis | Always offer alternatives |
| @-mentioning full doc files in descriptions | Bloats context budget | "Pitch" when to consult docs instead |
| Skills as CLAUDE.md replacement | Wrong scope — CLAUDE.md is for persistent context | Keep skills for specific workflows |
| `context: fork` without a task | Subagent gets guidelines but no actionable prompt | Only fork skills with explicit tasks |

---

## 8. Agent Skills Open Standard

Claude Code skills follow the [Agent Skills](https://agentskills.io) open standard, adopted by 30+ AI tools including Cursor, VS Code Copilot, Gemini CLI, OpenHands, Roo Code, and GitHub Copilot.

**Standard spec fields** (mandatory per spec):
- `name` — Required, must match directory name
- `description` — Required, max 1024 chars

**Claude Code extensions** beyond the standard:
- `disable-model-invocation` — Invocation control
- `user-invocable` — Menu visibility control
- `context` / `agent` — Subagent execution
- `model` — Model override
- `hooks` — Lifecycle hooks
- `argument-hint` — Autocomplete hints
- `!`command`` — Dynamic shell injection

This means skills authored for Claude Code are largely portable to other Agent Skills-compatible tools, with Claude-specific extensions gracefully ignored.

---

## 9. Migration from Commands to Skills

`.claude/commands/deploy.md` and `.claude/skills/deploy/SKILL.md` both create `/deploy`. Existing command files keep working. To migrate:

1. Create `.claude/skills/<name>/` directory
2. Move `<name>.md` to `.claude/skills/<name>/SKILL.md`
3. Add YAML frontmatter (name, description)
4. Optionally add supporting files, scripts, references

Skills are recommended over commands because they support:
- Supporting file directories
- Auto-discovery by Claude
- Frontmatter for invocation control
- Agent Skills standard compatibility

---

## 10. Real-World Skill Examples

### Minimal Reference Skill
```yaml
---
name: api-conventions
description: API design patterns for this codebase
---
When writing API endpoints:
- Use RESTful naming
- Return consistent error formats
- Include request validation
```

### Issue Fixer with Arguments
```yaml
---
name: fix-issue
description: Fix a GitHub issue
disable-model-invocation: true
argument-hint: [issue-number]
---
Fix GitHub issue $ARGUMENTS following our coding standards.
1. Read the issue description
2. Understand the requirements
3. Implement the fix
4. Write tests
5. Create a commit
```

### Multi-Argument Migration Skill
```yaml
---
name: migrate-component
description: Migrate a component from one framework to another
argument-hint: [component] [from-framework] [to-framework]
---
Migrate the $0 component from $1 to $2.
Preserve all existing behavior and tests.
```

### Forked Research Skill
```yaml
---
name: deep-research
description: Research a topic thoroughly
context: fork
agent: Explore
---
Research $ARGUMENTS thoroughly:
1. Find relevant files using Glob and Grep
2. Read and analyze the code
3. Summarize findings with specific file references
```

### Dynamic Context PR Skill
```yaml
---
name: pr-summary
description: Summarize changes in a pull request
context: fork
agent: Explore
allowed-tools: Bash(gh *)
---
## Pull request context
- PR diff: !`gh pr diff`
- PR comments: !`gh pr view --comments`
- Changed files: !`gh pr diff --name-only`

## Your task
Summarize this pull request...
```

### Read-Only Explorer
```yaml
---
name: safe-reader
description: Read files without making changes
allowed-tools: Read, Grep, Glob
---
Explore the codebase without making any changes.
```

---

## Sources

### Official Documentation
- [Extend Claude with skills - Claude Code Docs](https://code.claude.com/docs/en/skills)
- [Slash commands - Claude Code Docs](https://code.claude.com/docs/en/slash-commands)
- [Agent Skills Specification](https://agentskills.io/specification)
- [Agent Skills Overview](https://agentskills.io)
- [Anthropic Example Skills Repository](https://github.com/anthropics/skills)

### Community & Analysis
- [Claude Code Customization Guide](https://alexop.dev/posts/claude-code-customization-guide-claudemd-skills-subagents/) — Skills vs commands, subagent integration
- [How I Use Every Claude Code Feature](https://blog.sshh.io/p/how-i-use-every-claude-code-feature) — Practical tips, anti-patterns, security concerns
- [Awesome Claude Code](https://github.com/hesreallyhim/awesome-claude-code) — Curated skills, plugins, and tools
- [Claude Skills and Subagents: Escaping the Prompt Engineering Hamster Wheel](https://towardsdatascience.com/claude-skills-and-subagents-escaping-the-prompt-engineering-hamster-wheel/) — Delegation architecture patterns
- [Claude Code Agent Skills 2.0](https://medium.com/@richardhightower/claude-code-agent-skills-2-0-from-custom-instructions-to-programmable-agents-ab6e4563c176) — Advanced agent skill patterns
- [From Approval Hell to Just Do It](https://medium.com/@richardhightower/from-approval-hell-to-just-do-it-how-agent-skills-fork-governed-sub-agents-in-claude-code-2-1-c0438416433a) — context: fork and governed subagents
- [Claude Code Custom Commands: 3 Practical Examples](https://www.aiengineering.report/p/claude-code-custom-commands-3-practical) — When to (not) use commands
- [10 Must-Have Skills for Claude in 2026](https://medium.com/@unicodeveloper/10-must-have-skills-for-claude-and-any-coding-agent-in-2026-b5451b013051) — Community skill recommendations
