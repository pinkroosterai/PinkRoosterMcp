using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IWorkPackageTaskService
{
    Task<TaskResponse> CreateAsync(long projectId, int wpNumber, int phaseNumber, CreateTaskRequest request, string changedBy, CancellationToken ct = default);
    Task<TaskResponse?> UpdateAsync(long projectId, int wpNumber, int taskNumber, UpdateTaskRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(long projectId, int wpNumber, int taskNumber, CancellationToken ct = default);
    Task<TaskDependencyResponse> AddDependencyAsync(long projectId, int wpNumber, int taskNumber, ManageDependencyRequest request, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
    Task<bool> RemoveDependencyAsync(long projectId, int wpNumber, int taskNumber, long dependsOnTaskId, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
    Task<BatchUpdateTaskStatesResponse?> BatchUpdateStatesAsync(long projectId, int wpNumber, BatchUpdateTaskStatesRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
}
