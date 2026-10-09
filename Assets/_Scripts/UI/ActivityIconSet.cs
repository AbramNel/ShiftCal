using UnityEngine;
namespace ShiftCal.UI
{
    [CreateAssetMenu(menuName="ShiftCal/Activity icons")]
    public class ActivityIconSet : ScriptableObject
    {
        public static readonly string[] Ids={"note","appointment","medical","dentist","karate","school","pickup","birthday","vacation","sports","exercise","work","meeting","errands","vehicle","pet","family","home","reminder","activity"};
        public static readonly string[] Names={"Note","Appointment","Medical","Dentist","Karate","School","Pickup","Birthday","Vacation","Sports","Exercise","Work","Meeting","Errands","Vehicle","Pet","Family","Home","Reminder","Activity"};
        public static string Name(string id){int i=System.Array.IndexOf(Ids,id);return Names[i<0?19:i];}
        public Sprite[] sprites;
        public Sprite Get(string id){int i=System.Array.IndexOf(Ids,id);return sprites[Mathf.Max(0,i<0?19:i)];}
        public static ActivityIconSet Load()=>Resources.Load<ActivityIconSet>("ActivityIcons");
    }
}
