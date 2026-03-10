namespace PinkRooster.Shared.DTOs.Requests;

public sealed class PaginationRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
