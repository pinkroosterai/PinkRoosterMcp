using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class ActivityLogTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "get_activity_logs", ReadOnly = true,
        Title = "Get Activity Logs", OpenWorld = false)]
    [Description(
        "Returns recent API activity logs with pagination. " +
        "Use to audit recent API activity or debug issues with tool calls. " +
        "Each entry includes HTTP method, path, status code, duration, and caller identity.")]
    public async Task<string> GetActivityLogs(
        [Description("Page number. Default: 1.")] int page = 1,
        [Description("Number of items per page. Default: 25.")] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await apiClient.GetActivityLogsAsync(page, pageSize, ct);
        return JsonSerializer.Serialize(result, JsonSerializerOptions.Web);
    }
}
