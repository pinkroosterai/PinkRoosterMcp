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

        ## States (CompletionState)
        NotStarted, Designing, Implementing, Testing, InReview, Completed, Cancelled, Blocked, Replaced
        Categories — Active: Designing, Implementing, Testing, InReview | Inactive: NotStarted, Blocked | Terminal: Completed, Cancelled, Replaced

        ## States (FeatureStatus)
        Proposed, UnderReview, Approved, Scheduled, InProgress, Completed, Rejected, Deferred
        Categories — Active: UnderReview, Approved, Scheduled, InProgress | Inactive: Proposed, Deferred | Terminal: Completed, Rejected

        ## Other Enums
        - Priority: Critical, High, Medium, Low
        - IssueType: Bug, Defect, Regression, TechnicalDebt, PerformanceIssue, SecurityVulnerability
        - IssueSeverity: Critical, Major, Minor, Trivial
        - WorkPackageType: Feature, BugFix, Refactor, Spike, Chore
        - FeatureCategory: Feature, Enhancement, Improvement
        - VerificationMethod: AutomatedTest, Manual, AgentReview
        - StateFilterCategory: Active, Inactive, Terminal (used by get_issue_overview, get_feature_requests, get_work_packages)
        - EntityTypeFilter: Task, Wp, Issue, FeatureRequest (used by get_next_actions)
        - DependencyAction: Add, Remove (used by manage_dependency)
        - DeleteEntityType: Issue, FeatureRequest, WorkPackage, Phase, Task (used by delete_entity)

        ## Write Operations
        Return OperationResult JSON with responseType (Success/Warning/Error), message, optional id, and optional stateChanges array.
        State changes report automatic cascades: auto-block on dependency add, auto-unblock on blocker completion, phase/WP auto-complete when all children reach terminal state.
        create_or_update_memory uses upsert-by-name: if a memory with the same name exists, content is appended and tags are unioned. Response includes wasMerged flag.
        delete_entity and delete_memory are destructive and cannot be undone.
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
