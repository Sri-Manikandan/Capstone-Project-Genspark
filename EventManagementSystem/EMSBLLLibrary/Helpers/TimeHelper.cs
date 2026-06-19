namespace EMSBLLLibrary.Helpers
{
    public static class TimeHelper
    {
        private static readonly TimeZoneInfo Ist = FindIst();

        private static TimeZoneInfo FindIst()
        {
            foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
            }
            throw new InvalidOperationException("IST timezone (Asia/Kolkata) not found on this system.");
        }

        // Treats incoming unspecified/local DateTime as IST and returns UTC equivalent.
        // If already UTC, returns as-is.
        public static DateTime AssumeIstToUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), Ist);
        }

        // Converts a UTC DateTime to IST for API responses.
        public static DateTime UtcToIst(DateTime utcDt)
        {
            var utc = utcDt.Kind == DateTimeKind.Utc
                ? utcDt
                : DateTime.SpecifyKind(utcDt, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, Ist);
        }

        public static DateTime? UtcToIst(DateTime? utcDt) =>
            utcDt.HasValue ? UtcToIst(utcDt.Value) : null;
    }
}
