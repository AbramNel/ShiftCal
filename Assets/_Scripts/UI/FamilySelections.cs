using System.Collections.Generic;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Data;
namespace ShiftCal.UI
{
    public static class FamilySelections
    {
        public static string Me(ScheduleSave save)
        {
            if(save.account=="local")return save.profiles.Find(p=>p.active&&p.id==DeviceCalendarStore.State?.localProfileId)?.id;
            var uid=FamilyWorkbench.Uid;return string.IsNullOrEmpty(uid)?null:save.profiles.Find(p=>p.active&&p.linkedUid==uid)?.id;
        }
        public static List<SelectionItem> People(ScheduleSave save,bool assignment=false,IEnumerable<string> retained=null)
        {
            var options=new List<SelectionItem>();
            if(assignment){options.Add(new SelectionItem("Unassigned",null));options.Add(new SelectionItem("Me","@me",save.account=="local"?"Uses this device's selected profile":"Uses your linked family profile"){color=save.profiles.Find(p=>p.id==Me(save))?.color});}
            foreach(var p in save.profiles.Where(p=>p.active||retained?.Contains(p.id)==true))options.Add(new SelectionItem(p.name,p.id,p.active?null:"Archived profile"){color=p.color});
            return options;
        }
        public static void NeedsProfile(UnityEngine.UI.Text message)
        {
            message.text=AppSession.Instance.Data.account=="local"?"Choose your device profile in Family profiles, then try Me again.":"Ask the calendar owner to link your signed-in account in Family profiles.";
        }
        public static string Assignment(SelectionField field,ScheduleSave save)=>field.options[field.value].id=="@me"?Me(save):field.options[field.value].id;
    }
}
