using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Middleware;
using PinkRooster.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

// JSON options for MCP tool parameter marshalling — enables enum schema constraints
var toolSerializerOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
{
    Converters = { new JsonStringEnumConverter() },
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PropertyNameCaseInsensitive = true
};

// MCP Server with HTTP/SSE transport
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new() { Name = "PinkRooster", Version = "1.0.0" };
    // NOTE: Keep this in sync with the tools registered via WithToolsFromAssembly() in the Tools/ directory.
    options.ServerInstructions = """
        PinkRooster is a project management system for AI-assisted development workflows.

        ## Workflow
        1. Call get_project_status with the project's filesystem path to get its ID and current status.
        2. Use the returned project ID (e.g. 'proj-1') with all other tools.
        3. Call get_next_actions to see priority-ordered work items.

        ## Tools (24 total)
        Read: get_project_status, get_next_actions, get_issue_details, get_issue_overview, get_work_packages, get_work_package_details, get_feature_requests, get_feature_request_details, list_memories, get_memory_details
        Write: create_or_update_project, create_or_update_issue, create_or_update_work_package, scaffold_work_package, create_or_update_phase, create_or_update_task, batch_update_task_states, manage_dependency, create_or_update_feature_request, manage_user_stories, create_or_update_memory, verify_acceptance_criteria
        Destructive: delete_entity, delete_memory

        ## ID Formats
        - Project: proj-{N} (e.g. proj-1)
        - Issue: proj-{N}-issue-{N} (e.g. proj-1-issue-3)
        - Feature Request: proj-{N}-fr-{N} (e.g. proj-1-fr-3)
        - Work Package: proj-{N}-wp-{N} (e.g. proj-1-wp-2)
        - Phase: proj-{N}-wp-{N}-phase-{N} (e.g. proj-1-wp-2-phase-1)
        - Task: proj-{N}-wp-{N}-task-{N} (e.g. proj-1-wp-2-task-5)
        - Memory: proj-{N}-mem-{N} (e.g. proj-1-mem-1)

        ## State Categories
        States have three categories — Active, Inactive, Terminal — used by stateFilter params on list tools.

        ## Common Workflows

        ### Creating structured work from an issue
        1. get_issue_details → understand the problem
        2. scaffold_work_package with linkedIssueIds → creates WP + phases + tasks in one call
        3. get_work_package_details → verify the scaffold result

        ### Implementing a work package
        1. get_work_package_details → review phases and tasks
        2. create_or_update_task (state: Implementing) → start a task
        3. batch_update_task_states → complete multiple tasks (triggers phase/WP auto-complete cascade)

        ### Dependency management
        1. manage_dependency (action: Add) → auto-blocks dependent if blocker is non-terminal
        2. Complete blocker via state update → dependents auto-unblock to their previous active state
        3. Check stateChanges in response for cascade effects

        ### Adding detail to a feature request
        1. create_or_update_feature_request → set core fields
        2. manage_user_stories (action: Add) → add structured user stories one at a time

        ## Operational Constraints
        - batch_update_task_states: all tasks must belong to the same work package
        - scaffold_work_package task dependencies (dependsOnTaskIndices): 0-based, within same phase only
        - create_or_update_memory: upsert-by-name — same name appends content and unions tags
        - Update operations use PATCH semantics: null = "don't change", not "clear"
        - linkedIssueIds/linkedFeatureRequestIds on WP update: replaces ALL links (not additive)

        ## Write Responses
        Return OperationResult JSON with responseType (Success/Warning/Error), message, optional id, and optional stateChanges array.
        State changes report automatic cascades: auto-block on dependency add, auto-unblock on blocker completion, phase/WP auto-complete when all children reach terminal state.

        ## Error Recovery
        - Circular dependency → restructure task/WP ordering
        - Entity not found → verify ID format matches patterns above, check get_project_status for valid IDs
        - "already exists" on create → use the returned ID to update instead
        - delete_entity and delete_memory are destructive and cannot be undone
        """;
})
.WithHttpTransport()
.WithToolsFromAssembly(serializerOptions: toolSerializerOptions);

// Typed HTTP client for API Server communication
builder.Services.AddHttpClient<PinkRoosterApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiServer:BaseUrl"]
        ?? throw new InvalidOperationException("ApiServer:BaseUrl is not configured."));
    client.DefaultRequestHeaders.Add(
        AuthConstants.ApiKeyHeaderName,
        builder.Configuration["ApiServer:ApiKey"]
            ?? throw new InvalidOperationException("ApiServer:ApiKey is not configured."));
});

var app = builder.Build();

app.UseMiddleware<McpApiKeyAuthMiddleware>();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));
app.MapMcp();

app.Run();
