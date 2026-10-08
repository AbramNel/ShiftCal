using System;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    // Appearance belongs to the device, independently of account/calendar data.
    [ExecuteAlways]
    public class ThemeManager : MonoBehaviour
    {
        public enum Theme { MidnightGraphite, DeepTeal, SoftDaylight }
        public enum Role { Background, Surface, Input, Text, Muted, Accent, Elevated, Border, Warning, Destructive, Selection, DimmedDate, OnAccent }
        public Role role;
        public const string PreferenceKey = "ShiftCal.Appearance.v1";
        public static Theme Current { get; private set; } = Theme.MidnightGraphite;
        public static bool IsDark => Current != Theme.SoftDaylight;
        public static event Action Changed;
        public static readonly string[] Names = { "Midnight Graphite", "Deep Teal", "Soft Daylight" };
        private static bool initialized;
        private static readonly string[][] Palettes = {
            new[]{"#171B22","#232933","#303846","#F2F5F9","#ADB9CA","#9BD5FF","#2A3240","#435064","#FFD28A","#FFA0AF","#A1DBFF","#05091070","#102237"},
            new[]{"#10292E","#193B41","#234C53","#EDF9F6","#ACCDC9","#A4EBD4","#20464D","#41666B","#FFD594","#FFA9B0","#B9FFE4","#03171D75","#103C35"},
            new[]{"#F4F3EF","#FFFFFF","#EBEEF2","#17263C","#57667B","#245EA0","#F9FAFC","#D3DBE4","#92600C","#B62C45","#184E99","#17223248","#FFFFFF"}
        };
        public static Color Token(Role token) { ColorUtility.TryParseHtmlString(Palettes[(int)Current][(int)token], out var c); return c; }
        public static void Initialize(bool legacyDark)
        {
            if (initialized) return;
            initialized = true;
            Select((Theme)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, legacyDark ? 0 : 2), 0, 2));
        }
        public static void Select(Theme theme)
        {
            PlayerPrefs.SetInt(PreferenceKey, (int)theme); PlayerPrefs.Save(); Apply(theme);
        }
        // Compatibility for older callers/tests; this overload does not persist preferences.
        public static void Apply(bool dark) => Apply(dark ? Theme.MidnightGraphite : Theme.SoftDaylight);
        public static void Apply(Theme theme)
        {
            Current = theme;
            foreach (var t in UnityEngine.Object.FindObjectsByType<ThemeManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.Paint();
            App.AndroidBridge.Call<string>("themeBars", IsDark, "#" + ColorUtility.ToHtmlStringRGB(Token(Role.Background)));
            Changed?.Invoke();
        }
        private void OnEnable() => Paint();
        public void Paint()
        {
            var g = GetComponent<Graphic>(); if (g != null) g.color = Token(role);
            var outline = GetComponent<Outline>(); if (outline != null) outline.effectColor = Token(Role.Border);
        }
        public static Color ShiftColor(string hex)
        {
            var c = Core.ShiftStyleUtility.ToColor(hex);
            return IsDark ? Color.Lerp(Token(Role.Surface), c, .57f) : Color.Lerp(Color.white, c, .74f);
        }
        private static float Luminance(Color color)
        {
            var linear=color.linear;return linear.r*.2126f+linear.g*.7152f+linear.b*.0722f;
        }
        public static Color Legible(Color background)
        {
            var dark=new Color(.06f,.11f,.17f);var light=new Color(.97f,.98f,1);
            float luminance=Luminance(background);
            float darkContrast=(luminance+.05f)/(Luminance(dark)+.05f),lightContrast=(Luminance(light)+.05f)/(luminance+.05f);
            return darkContrast>lightContrast?dark:light;
        }
    }
}
