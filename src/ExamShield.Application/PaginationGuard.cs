namespace ExamShield.Application;

internal static class PaginationGuard
{
    internal const int MaxPageSize = 200;

    internal static void Validate(int page, int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), page,
                "Page must be 1 or greater.");
        if (pageSize < 1 || pageSize > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize,
                $"PageSize must be between 1 and {MaxPageSize}.");
    }
}
