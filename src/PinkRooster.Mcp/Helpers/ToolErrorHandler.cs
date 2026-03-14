using System.Text.Json;
using PinkRooster.Mcp.Responses;

namespace PinkRooster.Mcp.Helpers;

internal static class ToolErrorHandler
{
    internal static async Task<string> ExecuteAsync(Func<Task<string>> action, string operationName)
    {
        try
        {
            return await action();
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Error($"API call failed during {operationName}: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return OperationResult.Error($"Invalid response format during {operationName}: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Error($"{operationName} was cancelled.");
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Unexpected error during {operationName}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
