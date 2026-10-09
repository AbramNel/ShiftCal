using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class ShiftDropdown : Dropdown
    {
        public List<Color> shiftColors = new List<Color>();
        private int itemIndex;
        protected override GameObject CreateDropdownList(GameObject template)
        {
            itemIndex = 0; return base.CreateDropdownList(template);
        }
        protected override DropdownItem CreateItem(DropdownItem template)
        {
            var item = base.CreateItem(template);
            var swatch = item.transform.Find("Shift option color")?.GetComponent<Image>();
            if (swatch != null && itemIndex < shiftColors.Count) swatch.color = shiftColors[itemIndex];
            itemIndex++; return item;
        }
    }
}
