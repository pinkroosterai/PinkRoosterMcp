using ModelContextProtocol;
using PinkRooster.Mcp.Clients;
using PinkRooster.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

// MCP Server with HTTP/SSE transport
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new() { Name = "PinkRooster", Version = "1.0.0" };
    options.ServerInstructions = "PinkRooster MCP server providing project management tools.";
})
.WithHttpTransport()
.WithToolsFromAssembly();

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

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));
app.MapMcp();

app.Run();
