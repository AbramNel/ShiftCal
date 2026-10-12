using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class ShiftDropdown : SelectionField
    {
        public List<Color> shiftColors = new List<Color>();
        public override List<SelectionItem> Items
        {
            get { for(int i=0;i<options.Count&&i<shiftColors.Count;i++)options[i].color="#"+ColorUtility.ToHtmlStringRGB(shiftColors[i]);return options; }
        }
    }
}
