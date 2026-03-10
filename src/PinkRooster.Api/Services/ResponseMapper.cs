using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

/// <summary>
/// Centralises entity-to-DTO mapping for TaskResponse, PhaseResponse, and related types
/// so that WorkPackageService, PhaseService, and WorkPackageTaskService share one implementation.
/// </summary>
public static class ResponseMapper
{
    public static FileReferenceDto MapFileReference(FileReference f) => new()
    {
        FileName = f.FileName,
        RelativePath = f.RelativePath,
        Description = f.Description
    };

    public static List<FileReferenceDto> MapFileReferences(IEnumerable<FileReference> files) =>
        files.Select(MapFileReference).ToList();

    public static TaskDependencyResponse MapBlockedByDependency(
        WorkPackageTaskDependency d, long projectId, int wpNumber) => new()
    {
        TaskId = $"proj-{projectId}-wp-{wpNumber}-task-{d.DependsOnTask.TaskNumber}",
        Name = d.DependsOnTask.Name,
        State = d.DependsOnTask.State.ToString(),
        Reason = d.Reason
    };

    public static TaskDependencyResponse MapBlockingDependency(
        WorkPackageTaskDependency d, long projectId, int wpNumber) => new()
    {
        TaskId = $"proj-{projectId}-wp-{wpNumber}-task-{d.DependentTask.TaskNumber}",
        Name = d.DependentTask.Name,
        State = d.DependentTask.State.ToString(),
        Reason = d.Reason
    };

    public static TaskResponse MapTask(
        WorkPackageTask t, long projectId, int wpNumber, int phaseNumber) => new()
    {
        TaskId = $"proj-{projectId}-wp-{wpNumber}-task-{t.TaskNumber}",
        Id = t.Id,
        TaskNumber = t.TaskNumber,
        PhaseId = $"proj-{projectId}-wp-{wpNumber}-phase-{phaseNumber}",
        Name = t.Name,
        Description = t.Description,
        SortOrder = t.SortOrder,
        ImplementationNotes = t.ImplementationNotes,
        State = t.State.ToString(),
        PreviousActiveState = t.PreviousActiveState?.ToString(),
        StartedAt = t.StartedAt,
        CompletedAt = t.CompletedAt,
        ResolvedAt = t.ResolvedAt,
        TargetFiles = MapFileReferences(t.TargetFiles),
        Attachments = MapFileReferences(t.Attachments),
        BlockedBy = t.BlockedBy.Select(d => MapBlockedByDependency(d, projectId, wpNumber)).ToList(),
        Blocking = t.Blocking.Select(d => MapBlockingDependency(d, projectId, wpNumber)).ToList(),
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    public static AcceptanceCriterionDto MapAcceptanceCriterion(AcceptanceCriterion ac) => new()
    {
        Name = ac.Name,
        Description = ac.Description,
        VerificationMethod = ac.VerificationMethod,
        VerificationResult = ac.VerificationResult,
        VerifiedAt = ac.VerifiedAt
    };

    public static PhaseResponse MapPhase(WorkPackagePhase p, long projectId, int wpNumber) => new()
    {
        PhaseId = $"proj-{projectId}-wp-{wpNumber}-phase-{p.PhaseNumber}",
        Id = p.Id,
        PhaseNumber = p.PhaseNumber,
        Name = p.Name,
        Description = p.Description,
        SortOrder = p.SortOrder,
        State = p.State.ToString(),
        Tasks = p.Tasks.Select(t => MapTask(t, projectId, wpNumber, p.PhaseNumber)).ToList(),
        AcceptanceCriteria = p.AcceptanceCriteria.Select(MapAcceptanceCriterion).ToList(),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
