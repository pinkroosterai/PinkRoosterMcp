# Research: Best Practices for Writing Tool/Parameter Descriptions for AI Agents

> Research date: 2026-03-10 | Confidence: High (primary source is Anthropic's own engineering guidance)

## Executive Summary

Tool and parameter descriptions are **the single highest-leverage optimization** for AI agent tool-calling reliability. They function as prompt engineering for tools — the LLM uses them to decide which tool to call, what arguments to pass, and how to interpret results. Small refinements yield dramatic improvements. The key principles are: be specific, be explicit, think like an onboarding manager, and validate through evaluation.

---

## 1. Tool Naming

### Rules
- **Use verb-noun format** that conveys intent: `search_contacts` not `list_contacts`, `schedule_event` not `create_event`
- **Be specific**: `get_current_shipping_status` not `get_data`
- **Namespace related tools** with consistent prefixes by service (`asana_search`, `jira_search`) or resource (`asana_projects_search`, `asana_users_search`) — helps agents disambiguate when many tools are available
- **Match regex** `^[a-zA-Z0-9_-]{1,64}$` (universal convention across providers)

### Anti-patterns
- Generic names like `run`, `execute`, `do_action`
- Names that overlap with other tools without clear differentiation
- Abbreviations that obscure meaning (`upd_proj` vs `update_project`)

---

## 2. Tool Descriptions

### The Golden Rule
> "Think of how you would describe your tool to a new hire on your team. Consider the context that you might implicitly bring — specialized query formats, definitions of niche terminology, relationships between underlying resources — and make it explicit." — Anthropic Engineering

### Structure of an Effective Description

A tool description should answer these questions in order:
1. **What does it do?** (primary function)
2. **When should the agent use it?** (usage context / triggers)
3. **When should the agent NOT use it?** (disambiguation from similar tools)
4. **What does the response contain?** (brief output shape — helps agents plan multi-step workflows)

### Good vs Bad Examples

```
BAD:  "Gets data"
GOOD: "Retrieves the current shipping status, tracking number, and estimated
       delivery date for a customer order by its order ID"

BAD:  "Search function"
GOOD: "Search the product catalog for items matching a query. Use this for
       product-related questions. Do NOT use this for order status or account
       questions — use get_order_status or get_account_info instead."

BAD:  "Returns project info"
GOOD: "Returns a compact project status summary with issue/WP counts by state
       category and active/inactive/blocked item lists. Call first when starting
       work on a project."
```

### Key Techniques

1. **State the workflow position**: "Use after `get_project_status` to decide what to work on next" — helps agents understand tool sequencing
2. **Mention what's NOT included**: "Returns issue counts but not full issue details — use `get_issue_details` for that" — prevents wrong tool selection
3. **Include format hints for responses**: "Returns a flat JSON array sorted by priority" — agents plan better when they know response shapes
4. **Keep it concise but complete**: Descriptions consume tokens on every request. Balance completeness with brevity — every word should earn its place

### Token Awareness
Tool descriptions are included in every API request context. For a server with 16 tools, overly verbose descriptions compound. Aim for 1-3 sentences per tool. If complex usage patterns exist, use the MCP server `instructions` field (sent once during initialization) for extended guidance rather than per-tool descriptions.

---

## 3. Parameter Descriptions

### Naming
- **Be unambiguous**: `user_id` not `user`, `project_path` not `path`
- **Use the domain language**: Match what the API/system actually calls things
- **Avoid overloaded terms**: If `id` could mean database ID or human-readable ID, be explicit: `projectId` with description "Project ID (e.g. 'proj-1')"

### Description Content

Every parameter description should include:
1. **What it represents** (semantic meaning)
2. **Expected format** with example (critical for strings)
3. **Constraints** (min/max, allowed values, optionality)

```
BAD:  "date": { "type": "string", "description": "Date" }
GOOD: "date": { "type": "string", "description": "Date in YYYY-MM-DD format (e.g., 2026-03-04)" }

BAD:  "id": { "type": "string", "description": "The ID" }
GOOD: "projectId": { "type": "string", "description": "Project ID (e.g. 'proj-1')." }

BAD:  "filter": { "type": "string", "description": "Filter to apply" }
GOOD: "stateFilter": { "type": "string", "description": "Filter by state category: 'active', 'inactive', or 'terminal'. Omit for all." }
```

### Use Enums for Constrained Values
When a parameter accepts a fixed set of values, **always use `enum`** rather than describing options in the description text. Enums are structurally enforced and dramatically reduce invalid inputs:

```json
"status_filter": {
  "type": "string",
  "enum": ["pending", "shipped", "delivered", "returned"],
  "description": "Filter orders by status"
}
```

### Required vs Optional
- Mark truly required parameters with `required`
- Use sensible defaults for optional parameters and document them: "Maximum number of items to return. Default 10."
- Don't make parameters required if there's a natural default — this reduces friction for agents

### Typed Parameters vs JSON Strings
**Never use JSON-encoded string parameters** when structured types are available. Instead of:
```
"tasks": { "type": "string", "description": "JSON array of task objects..." }
```
Use proper typed arrays/objects that the LLM schema can validate:
```
"tasks": { "type": "array", "items": { "type": "object", "properties": { ... } } }
```
JSON string parameters are an anti-pattern because:
- LLMs must generate valid JSON within a string (error-prone)
- No schema validation until runtime
- Descriptions must explain the schema verbally (token-expensive and ambiguous)

---

## 4. Response Design

### Return Human-Readable Identifiers
- Prefer `proj-1-wp-3` over `42` or `550e8400-e29b-41d4-a716-446655440000`
- Human-readable IDs "significantly improve precision in retrieval tasks" (Anthropic)
- Include both semantic names AND IDs so agents can reason about entities while passing IDs to subsequent tools

### Optimize for Token Efficiency
- **Offer response format options** via a parameter: agents can request `"concise"` (72 tokens) vs `"detailed"` (206 tokens) based on their needs
- **Filter out unnecessary metadata**: raw technical properties, internal timestamps, and debug info waste agent context
- **Truncate with guidance**: If results are large, truncate and include instructions: "Results truncated. Use filters or pagination for more specific results."
- **Omit null/empty fields**: Use `JsonIgnoreCondition.WhenWritingNull` to skip irrelevant data

### Actionable Error Messages
```
BAD:  "Error: 400"
BAD:  "Invalid parameter"
GOOD: "Invalid project ID 'foo'. Expected format: 'proj-{number}' (e.g., 'proj-1')."
GOOD: "Circular dependency detected: proj-1-wp-3 → proj-1-wp-5 → proj-1-wp-3. Remove an existing dependency first."
```

Errors should tell the agent **what went wrong** and **what to do next**.

---

## 5. Tool Granularity & Organization

### Fewer, Smarter Tools
> "More tools != better performance. Use fewer tools." — Anthropic

- Consolidate multi-step operations: A `scaffold_work_package` that creates WP + phases + tasks in one call is far better than 4-10 sequential tool calls
- Prefer `search_logs` (returns relevant lines with context) over `read_logs` (dumps everything)
- Each tool should represent a **complete cognitive unit** — something an agent would think of as a single action

### When to Split vs Merge
- **Split** when tools serve genuinely different purposes or audiences (read vs write)
- **Merge** when agents always chain tools in the same sequence
- **Split by entity domain** for clarity: `ProjectTools`, `IssueTools`, `WorkPackageTools`

### Avoid Overlap
If `get_project_status` returns issue counts and `get_issue_overview` also returns issue lists, make the distinction crystal clear in descriptions. Agents confused by overlapping tools will waste calls.

---

## 6. MCP-Specific Guidance

### Tool Annotations (MCP 2025-06-18)
The MCP spec supports `annotations` on tools for metadata:
- `title`: Human-readable display name (separate from the machine `name`)
- `readOnlyHint`: Whether the tool only reads data (no side effects)
- `destructiveHint`: Whether the tool may perform destructive operations
- `idempotentHint`: Whether calling the tool repeatedly has the same effect
- `openWorldHint`: Whether the tool interacts with external entities

These annotations help clients display appropriate UI (confirmation dialogs for destructive tools, etc.).

### Server Instructions
The MCP `instructions` field in server metadata (sent once during `initialize`) is ideal for:
- Cross-tool workflow guidance ("Call `get_project_status` first, then use the returned ID with other tools")
- Domain terminology definitions
- Response format conventions
- Error handling patterns

This avoids repeating guidance in every tool description.

### Output Schema
MCP now supports `outputSchema` on tools. When provided:
- Servers MUST return `structuredContent` conforming to the schema
- Helps clients and LLMs understand and validate responses
- Enables stronger type integration

---

## 7. Evaluation & Iteration

### Test with Real Agent Workflows
- Design evaluation tasks requiring multiple chained tool calls
- Measure: accuracy, total tool calls, token consumption, runtime, error rate
- Track common workflows agents pursue — opportunities to consolidate tools

### Use Agent Feedback
- Enable chain-of-thought/reasoning output during evaluation
- Agents are "remarkably effective at spotting contradictions and inefficient patterns" in tool descriptions
- What agents **omit** in feedback is often more important than what they include

### Iterate on Descriptions
- Small description tweaks can fix entire categories of failures (e.g., adding "Do NOT use for X" resolved a persistent wrong-tool-selection issue)
- A/B test description variants with evaluation suites
- Log every tool decision for post-hoc analysis

---

## 8. Checklist for Tool Authors

For each tool, verify:

- [ ] **Name** clearly conveys the action (verb-noun pattern)
- [ ] **Description** explains what, when to use, when NOT to use
- [ ] **Description** mentions workflow position ("use after X", "use before Y")
- [ ] **Parameters** have unambiguous names (not `user`, `data`, `input`)
- [ ] **Parameter descriptions** include format examples for strings
- [ ] **Enums** used for any fixed-value parameters
- [ ] **Required** array only includes truly mandatory parameters
- [ ] **Defaults** documented for optional parameters
- [ ] **No JSON-string parameters** — use typed schemas instead
- [ ] **Error responses** are actionable (what went wrong + what to do next)
- [ ] **Response** uses human-readable IDs and omits unnecessary fields
- [ ] **Description** fits in 1-3 sentences (token-conscious)

---

## Sources

1. **Anthropic Engineering** — "Writing effective tools for AI agents—using AI agents" (2025)
   https://www.anthropic.com/engineering/writing-tools-for-agents

2. **Anthropic Docs** — "Tool use with Claude" (2026)
   https://platform.claude.com/docs/en/agents-and-tools/tool-use/overview

3. **MCP Specification** — Tools (Protocol Revision 2025-06-18)
   https://modelcontextprotocol.io/specification/2025-06-18/server/tools

4. **Martin Fowler** — "Function calling using LLMs"
   https://martinfowler.com/articles/function-call-LLM.html

5. **ztabs.co** — "Function Calling in LLMs: How AI Agents Use Tools (2026 Guide)"
   https://ztabs.co/blog/function-calling-llm-guide

6. **Laurent Kubaski** — "Tool (aka Function Calling) Best Practices" (2025)
   https://medium.com/@laurentkubaski/tool-or-function-calling-best-practices-a5165a33d5f1

7. **Runloop AI** — "Mastering LLM Function Calling"
   https://runloop.ai/blog/mastering-llm-function-calling-a-guide-to-enhancing-ai-capabilities

8. **codewithcaptain.com** — "LLM function calling best practices: build reliable agents"
   https://codewithcaptain.com/llm-function-calling-best-practices/
