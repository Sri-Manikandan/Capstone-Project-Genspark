using System.Security.Claims;

namespace EMSApplicationLayer.Helpers
{
    public static class ClaimsHelper
    {
        public static int GetUserId(ClaimsPrincipal user)
        {
            var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? user.FindFirstValue("sub");
            return int.Parse(sub ?? "0");
        }

        public static string GetUserRole(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Role) ?? "User";
        }
    }
}
