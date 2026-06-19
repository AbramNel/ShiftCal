using System;
using System.Globalization;

namespace ShiftCal.Core
{
    public static class ShiftTimeUtility
    {
        private static readonly string[] TimeFormats =
        {
            "h:mmtt", "h:mm tt", "htt", "h tt",
            "hh:mmtt", "hh:mm tt", "HH:mm", "H:mm"
        };

        public static bool TryCalculateHours(string startTime, string endTime, out float hours)
        {
            hours = 0f;

            if (string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
                return true;

            if (!TryParseTime(startTime, out TimeSpan start) || !TryParseTime(endTime, out TimeSpan end))
                return false;

            TimeSpan duration = end - start;
            if (duration.TotalMinutes < 0)
                duration = duration.Add(TimeSpan.FromDays(1));

            hours = (float)Math.Round(duration.TotalHours, 2);
            return true;
        }

        public static string FormatHours(float hours)
        {
            return hours <= 0f ? string.Empty : hours.ToString("0.##", CultureInfo.InvariantCulture) + "h";
        }

        private static bool TryParseTime(string value, out TimeSpan time)
        {
            time = default;
            string normalized = value.Trim().Replace(".", string.Empty).ToUpperInvariant();
            normalized = normalized.Replace("AM", " AM").Replace("PM", " PM");
            normalized = normalized.Replace("  ", " ");

            foreach (string format in TimeFormats)
            {
                if (DateTime.TryParseExact(normalized, format, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime parsed))
                {
                    time = parsed.TimeOfDay;
                    return true;
                }
            }

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime fallback))
            {
                time = fallback.TimeOfDay;
                return true;
            }

            return false;
        }
    }
}
