using System.Text.Json;
using System.Text.Json.Serialization;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Mcp.Responses;

public record OperationResult(
    ResponseType ResponseType,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Id = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? NextStep = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    List<StateChangeDto>? StateChanges = null)
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Indented;

    public static string Success(string id, string message, string? nextStep = null,
        List<StateChangeDto>? stateChanges = null)
    {
        var changes = stateChanges is { Count: > 0 } ? stateChanges : null;
        return JsonSerializer.Serialize(
            new OperationResult(ResponseType.Success, message, id, nextStep, changes), JsonOptions);
    }

    /// <summary>Informational success without a specific entity ID.</summary>
    public static string SuccessMessage(string message) =>
        JsonSerializer.Serialize(new OperationResult(ResponseType.Success, message), JsonOptions);

    public static string Warning(string message, string? nextStep = null) =>
        JsonSerializer.Serialize(new OperationResult(ResponseType.Warning, message, NextStep: nextStep), JsonOptions);

    public static string Error(string message, string? nextStep = null) =>
        JsonSerializer.Serialize(new OperationResult(ResponseType.Error, message, NextStep: nextStep), JsonOptions);
}
