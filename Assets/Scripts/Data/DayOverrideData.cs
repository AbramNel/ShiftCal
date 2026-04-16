using System;

namespace ShiftCal.Data
{
    [Serializable]
    public class DayOverrideData
    {
        public string dateKey;
        public int shiftType;
        public string note;
        public string userId;
        public long updatedAt;
    }
}
