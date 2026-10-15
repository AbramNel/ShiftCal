using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShiftCal.Core;
using ShiftCal.Data;
namespace ShiftCal.App
{
    // Actual hours are private integer hundredths, never inferred from shifts.
    public static class WorkedHoursStore
    {
        public static event Action Changed;
        static DeviceCalendarState State=>DeviceCalendarStore.State?.account==AppSession.Instance?.Data?.account?DeviceCalendarStore.State:null;
        static string Group=>AppSession.Instance?.CurrentGroup?.groupId;
        public static bool TryParse(string text,out int value)
        {
            value=0;text=(text??"").Trim();if(text.Contains('.')&&text.Contains(','))return false;int dot=text.IndexOfAny(new[]{'.',','});if(dot>=0&&text.Length-dot-1>2)return false;
            if(!decimal.TryParse(text.Replace(',','.'),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var hours)||hours<0||hours>24||decimal.Round(hours,2)!=hours)return false;
            value=(int)(hours*100m);return true;
        }
        public static string Format(int hundredths)=>(hundredths/100m).ToString("0.##",CultureInfo.CurrentCulture);
        public static WorkedHoursEntry Get(string key)=>State?.workedHours?.Find(h=>h.groupId==Group&&h.dateKey==key);
        public static List<WorkedHoursEntry> Month(DateTime month)=>State?.workedHours?.Where(h=>h.groupId==Group&&h.dateKey.StartsWith(month.ToString("yyyy-MM-",CultureInfo.InvariantCulture),StringComparison.Ordinal)).OrderBy(h=>h.dateKey).ToList()??new List<WorkedHoursEntry>();
        public static int Total(DateTime month)=>Month(month).Sum(h=>h.hundredths);
        public static void Set(string key,int? hundredths)
        {
            DateKeyUtility.FromDateKey(key);if(State==null||string.IsNullOrEmpty(Group))throw new InvalidOperationException("Open a calendar before recording hours.");
            if(hundredths.HasValue&&(hundredths<0||hundredths>2400))throw new ArgumentException("Use 0–24 hours, with up to two decimal places.");
            var entries=State.workedHours??(State.workedHours=new List<WorkedHoursEntry>());var previous=entries.ToList();
            entries.RemoveAll(h=>h.groupId==Group&&h.dateKey==key);if(hundredths.HasValue)entries.Add(new WorkedHoursEntry{groupId=Group,dateKey=key,hundredths=hundredths.Value,updatedAt=DateKeyUtility.UnixMsNow()});
            try{DeviceCalendarStore.Write();}catch{State.workedHours=previous;throw;}
            Changed?.Invoke();
        }
    }
}
