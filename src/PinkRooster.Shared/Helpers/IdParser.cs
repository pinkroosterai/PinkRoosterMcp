namespace PinkRooster.Shared.Helpers;

public static class IdParser
{
    public static bool TryParseProjectId(string humanId, out long projectId)
    {
        projectId = 0;
        if (!humanId.StartsWith("proj-"))
            return false;

        return long.TryParse(humanId.AsSpan(5), out projectId) && projectId > 0;
    }

    public static bool TryParseIssueId(string humanId, out long projectId, out int issueNumber)
    {
        projectId = 0;
        issueNumber = 0;

        const string issueMarker = "-issue-";
        if (!humanId.StartsWith("proj-"))
            return false;

        var issueIndex = humanId.IndexOf(issueMarker, StringComparison.Ordinal);
        if (issueIndex < 0)
            return false;

        var projectPart = humanId.AsSpan(5, issueIndex - 5);
        var issuePart = humanId.AsSpan(issueIndex + issueMarker.Length);

        return long.TryParse(projectPart, out projectId) && projectId > 0
            && int.TryParse(issuePart, out issueNumber) && issueNumber > 0;
    }

    public static bool TryParseWorkPackageId(string humanId, out long projectId, out int wpNumber)
    {
        projectId = 0;
        wpNumber = 0;

        const string wpMarker = "-wp-";
        if (!humanId.StartsWith("proj-"))
            return false;

        var wpIndex = humanId.IndexOf(wpMarker, StringComparison.Ordinal);
        if (wpIndex < 0)
            return false;

        var projectPart = humanId.AsSpan(5, wpIndex - 5);
        var wpPart = humanId.AsSpan(wpIndex + wpMarker.Length);

        // Ensure no further segments (e.g., -phase- or -task-)
        if (wpPart.Contains('-'))
            return false;

        return long.TryParse(projectPart, out projectId) && projectId > 0
            && int.TryParse(wpPart, out wpNumber) && wpNumber > 0;
    }

    public static bool TryParsePhaseId(string humanId, out long projectId, out int wpNumber, out int phaseNumber)
    {
        projectId = 0;
        wpNumber = 0;
        phaseNumber = 0;

        const string wpMarker = "-wp-";
        const string phaseMarker = "-phase-";
        if (!humanId.StartsWith("proj-"))
            return false;

        var wpIndex = humanId.IndexOf(wpMarker, StringComparison.Ordinal);
        if (wpIndex < 0)
            return false;

        var phaseIndex = humanId.IndexOf(phaseMarker, StringComparison.Ordinal);
        if (phaseIndex < 0)
            return false;

        var projectPart = humanId.AsSpan(5, wpIndex - 5);
        var wpPart = humanId.AsSpan(wpIndex + wpMarker.Length, phaseIndex - wpIndex - wpMarker.Length);
        var phasePart = humanId.AsSpan(phaseIndex + phaseMarker.Length);

        return long.TryParse(projectPart, out projectId) && projectId > 0
            && int.TryParse(wpPart, out wpNumber) && wpNumber > 0
            && int.TryParse(phasePart, out phaseNumber) && phaseNumber > 0;
    }

    public static bool TryParseTaskId(string humanId, out long projectId, out int wpNumber, out int taskNumber)
    {
        projectId = 0;
        wpNumber = 0;
        taskNumber = 0;

        const string wpMarker = "-wp-";
        const string taskMarker = "-task-";
        if (!humanId.StartsWith("proj-"))
            return false;

        var wpIndex = humanId.IndexOf(wpMarker, StringComparison.Ordinal);
        if (wpIndex < 0)
            return false;

        var taskIndex = humanId.IndexOf(taskMarker, StringComparison.Ordinal);
        if (taskIndex < 0)
            return false;

        var projectPart = humanId.AsSpan(5, wpIndex - 5);
        var wpPart = humanId.AsSpan(wpIndex + wpMarker.Length, taskIndex - wpIndex - wpMarker.Length);
        var taskPart = humanId.AsSpan(taskIndex + taskMarker.Length);

        return long.TryParse(projectPart, out projectId) && projectId > 0
            && int.TryParse(wpPart, out wpNumber) && wpNumber > 0
            && int.TryParse(taskPart, out taskNumber) && taskNumber > 0;
    }
}
