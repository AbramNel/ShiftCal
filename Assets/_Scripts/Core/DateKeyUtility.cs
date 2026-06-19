using System;
using System.Globalization;

namespace ShiftCal.Core
{
    public static class DateKeyUtility
    {
        public const string DateFormat = "yyyy-MM-dd";

        public static string ToDateKey(DateTime date)
        {
            return date.Date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        public static DateTime FromDateKey(string dateKey)
        {
            return DateTime.ParseExact(dateKey, DateFormat, CultureInfo.InvariantCulture);
        }

        public static DateTime FirstVisibleGridDate(DateTime month)
        {
            DateTime firstOfMonth = new DateTime(month.Year, month.Month, 1);
            int offset = (int)firstOfMonth.DayOfWeek;
            return firstOfMonth.AddDays(-offset).Date;
        }

        public static long UnixMsNow()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
