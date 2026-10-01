using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class ThemeManager : MonoBehaviour
    {
        public enum Role { Background, Surface, Input, Text, Muted, Accent }
        public Role role;
        private static bool dark=true;
        public static bool IsDark=>dark;
        private void OnEnable(){Paint();}
        private void Paint()
        {
            Color c=role==Role.Background?(dark?Hex("#14171C"):Hex("#F5F7FA"))
                :role==Role.Surface?(dark?Hex("#22272E"):Color.white)
                :role==Role.Input?(dark?Hex("#30363D"):Hex("#E7ECF1"))
                :role==Role.Text?(dark?Hex("#F0F3F6"):Hex("#192027"))
                :role==Role.Muted?(dark?Hex("#B1BAC4"):Hex("#52606D"))
                :dark?Hex("#7DD3FC"):Hex("#03658C");
            var g=GetComponent<Graphic>();if(g!=null)g.color=c;
        }
        public static void Apply(bool value)
        {
            dark=value;foreach(var t in Object.FindObjectsByType<ThemeManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))t.Paint();
            App.AndroidBridge.Call<string>("systemBars",value);
        }
        private static Color Hex(string s){ColorUtility.TryParseHtmlString(s,out var c);return c;}
    }
}
