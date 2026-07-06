namespace EMSDALLibrary.Interfaces
{
    /// <summary>
    /// Resolves the current acting user for the changelog audit trail.
    /// Returns null when there is no authenticated HTTP user (background
    /// services, the seeder, webhooks, anonymous requests) — such changes
    /// are not audited.
    /// </summary>
    public interface ICurrentUserAccessor
    {
        int? GetUserId();
        string? GetUserRole();
    }
}
