using EMSDALLibrary.Interfaces;

namespace EMSApplicationLayer.Helpers
{
    /// <summary>
    /// Resolves the acting user from the current HTTP request's JWT claims.
    /// Returns null when there is no authenticated user, so background
    /// services, the seeder, webhooks, and anonymous requests are not audited.
    /// </summary>
    public class HttpContextCurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int? GetUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;
            return ClaimsHelper.GetUserId(user);
        }

        public string? GetUserRole()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;
            return ClaimsHelper.GetUserRole(user);
        }
    }
}
