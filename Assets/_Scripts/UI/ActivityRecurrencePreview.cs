using System;
using System.Linq;
using ShiftCal.Core;
using ShiftCal.Data;
namespace ShiftCal.UI
{
    public static class ActivityRecurrencePreview
    {
        public static string Describe(CalendarActivity draft)
        {
            var a=ActivityResolver.Clone(draft);a.title=string.IsNullOrWhiteSpace(a.title)?"Activity":a.title;
            if(!ActivityResolver.Validate(a,out var error))return error;
            if(a.recurrence==RecurrenceKind.Once)return "Only this date.";
            string unit=a.recurrence==RecurrenceKind.Daily?"day":a.recurrence==RecurrenceKind.Weekly?"week":"month";
            string rhythm="Repeats every "+(a.interval==1?unit:a.interval+" "+unit+"s");
            if(a.recurrence==RecurrenceKind.Weekly)rhythm+=" on "+string.Join(" and ",a.weekdays.OrderBy(i=>i).Select(i=>System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DayNames[i]));
            if(a.recurrence==RecurrenceKind.Monthly)rhythm+=" on day "+DateKeyUtility.FromDateKey(a.dateKey).Day+" (last day in shorter months)";
            if(a.hasShiftConstraint)rhythm+=" when the shift is "+(App.AppSession.Instance?.CurrentGroup.shiftTypes.Find(x=>x.id==a.requiredShiftType)?.name??"missing (choose an available shift)");
            var from=DateKeyUtility.FromDateKey(a.dateKey);if(from<DateTime.Today)from=DateTime.Today;
            var next=ActivityResolver.NextDates(a,from,3,App.AppSession.Instance?.Data).Select(d=>d.ToString("ddd, MMM d")).ToArray();
            if(!string.IsNullOrEmpty(a.until))rhythm+="; ends "+DateKeyUtility.FromDateKey(a.until).ToString("MMM d, yyyy");else if(a.count>0)rhythm+="; "+a.count+" occurrences";
            return rhythm+".\n"+(next.Length>0?"Next: "+string.Join(" · ",next):"No qualifying dates within the next three years or selected ending.");
        }
    }
}
