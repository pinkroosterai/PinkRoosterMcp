namespace PinkRooster.Shared.DTOs.Requests;

public sealed class PaginationRequest
{
    private int _page = 1;
    private int _pageSize = 25;

    public int Page
    {
        get => _page;
        init => _page = Math.Max(1, value);
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value, 1, 200);
    }
}
