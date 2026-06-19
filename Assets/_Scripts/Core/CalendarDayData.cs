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
        public string personName;
        public string note;
        public string shiftName;
        public string shiftColorHex;
        public string startTime;
        public string endTime;
        public float hours;

        public int ResolvedShift => hasOverride ? overrideShift : baseShift;
    }
}
