using System.Text.Json;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Mcp.Helpers;

internal static class McpInputParser
{
    private static readonly HashSet<string> TerminalStateStrings =
        CompletionStateConstants.TerminalStates.Select(s => s.ToString()).ToHashSet();

    internal static bool IsTerminalState(string state) =>
        TerminalStateStrings.Contains(state);

    internal static TEnum ParseEnumOrDefault<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : defaultValue;
    }

    internal static TEnum? ParseEnum<TEnum>(string value) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;
    }

    internal static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    internal static List<FileReferenceDto>? ParseFileReferences(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<FileReferenceDto>>(json, JsonDefaults.Indented);
        }
        catch
        {
            return null;
        }
    }

    internal static List<AcceptanceCriterionDto>? ParseAcceptanceCriteria(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<AcceptanceCriterionDto>>(json, JsonDefaults.Indented);
        }
        catch
        {
            return null;
        }
    }

    internal static List<CreateTaskRequest>? ParseCreateTasks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<CreateTaskRequest>>(json, JsonDefaults.Indented);
        }
        catch
        {
            return null;
        }
    }

    internal static List<UpsertTaskInPhaseDto>? ParseUpsertTasks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<UpsertTaskInPhaseDto>>(json, JsonDefaults.Indented);
        }
        catch
        {
            return null;
        }
    }

    internal static List<T>? NullIfEmpty<T>(List<T> list) =>
        list.Count == 0 ? null : list;
}
