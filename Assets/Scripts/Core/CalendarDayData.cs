using System;

namespace ShiftCal.Core
{
    [Serializable]
    public class CalendarDayData
    {
        public DateTime date;
        public string dateKey;
        public bool isCurrentMonth;
        public int baseShift;
        public int overrideShift;
        public bool hasOverride;
        public string note;

        public int ResolvedShift => hasOverride ? overrideShift : baseShift;
    }
}
