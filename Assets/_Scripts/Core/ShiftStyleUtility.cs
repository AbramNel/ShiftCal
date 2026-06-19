using System.Collections.Generic;
using UnityEngine;
using ShiftCal.Data;

namespace ShiftCal.Core
{
    public static class ShiftStyleUtility
    {
        public const string DefaultColorHex = "#EEF2F7";

        public static string GetName(int shiftType, List<ShiftTypeDefinitionData> definitions)
        {
            ShiftTypeDefinitionData definition = FindDefinition(shiftType, definitions);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.name))
                return definition.name;

            if (System.Enum.IsDefined(typeof(ShiftTypeId), shiftType))
                return ((ShiftTypeId)shiftType).ToString();

            return "Shift " + shiftType;
        }

        public static string GetColorHex(int shiftType, List<ShiftTypeDefinitionData> definitions)
        {
            ShiftTypeDefinitionData definition = FindDefinition(shiftType, definitions);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.colorHex))
                return definition.colorHex;

            switch ((ShiftTypeId)shiftType)
            {
                case ShiftTypeId.Day12:
                    return "#FBBF24";
                case ShiftTypeId.Day:
                    return "#F9D65C";
                case ShiftTypeId.Night:
                    return "#6D7DF2";
                case ShiftTypeId.Off:
                    return "#9FF4F1";
                case ShiftTypeId.Vacation:
                    return "#F59AC8";
                case ShiftTypeId.FillDay:
                    return "#F4A261";
                case ShiftTypeId.FillNight:
                    return "#7A5CFA";
                case ShiftTypeId.DaysMod:
                    return "#9FF4F1";
                default:
                    return DefaultColorHex;
            }
        }

        public static Color ToColor(string colorHex)
        {
            if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color color))
                return color;

            ColorUtility.TryParseHtmlString(DefaultColorHex, out Color fallback);
            return fallback;
        }

        private static ShiftTypeDefinitionData FindDefinition(int shiftType, List<ShiftTypeDefinitionData> definitions)
        {
            if (definitions == null) return null;
            return definitions.Find(definition => definition != null && definition.id == shiftType);
        }
    }
}
