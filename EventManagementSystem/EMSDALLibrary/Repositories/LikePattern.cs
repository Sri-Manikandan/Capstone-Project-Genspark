namespace EMSDALLibrary.Repositories
{
    /// <summary>
    /// Builds LIKE/ILIKE patterns for free-text search. Postgres treats % and _ as
    /// wildcards inside the pattern, so a raw user term like "50%" has to be escaped
    /// or it silently matches far more than the user asked for.
    /// </summary>
    internal static class LikePattern
    {
        public static string Contains(string term)
        {
            var escaped = term
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            return $"%{escaped}%";
        }
    }
}
