using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class ActivityLogTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "get_activity_logs", ReadOnly = true)]
    [Description("Get recent API activity logs with pagination. Returns a list of HTTP requests that have been made to the API server, including method, path, status code, duration, and caller identity.")]
    public async Task<string> GetActivityLogs(
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of items per page (default: 25)")] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await apiClient.GetActivityLogsAsync(page, pageSize, ct);
        return JsonSerializer.Serialize(result, JsonSerializerOptions.Web);
    }
}
